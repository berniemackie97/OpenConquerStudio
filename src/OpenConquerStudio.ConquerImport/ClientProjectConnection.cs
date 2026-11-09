using System.Collections.Immutable;
using System.Globalization;
using OpenConquer.Content;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Map;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio.ConquerImport;

public sealed record ClientAssetEntry(string Kind, string Path, uint? MapDataId = null, bool Available = true, uint? Appearance = null)
{
    public override string ToString() => MapDataId is uint id ? $"Map {id}: {Path}{(Available ? "" : " (source missing)")}" : Path;
}

internal sealed record ClientProjectSettings(int SchemaVersion, string RepositoryPath, string ContentPath);

/// <summary>Links an authoring workspace to a real client repository and its read-only source content.</summary>
public sealed class ClientProjectConnection
{
    private readonly ClientContentRoot _loose;
    private readonly GameMapCatalog _maps;

    private ClientProjectConnection(string repository, string contentRoot)
    {
        Repository = AssetFileIO.RequireSafePath(repository);
        ContentRoot = AssetFileIO.RequireSafePath(contentRoot);
        if (!File.Exists(Path.Combine(Repository, "src", "OpenConquer.Client", "OpenConquer.Client.csproj")))
        {
            throw new InvalidDataException("Select the OpenConquerClient repository containing src/OpenConquer.Client/OpenConquer.Client.csproj.");
        }

        _loose = new ClientContentRoot(ContentRoot);
        Source = PackagedClientContentSource.Open(ContentRoot);
        _maps = GameMapCatalog.Load(Source);
    }

    public string Repository
    {
        get;
    }
    public string ContentRoot
    {
        get;
    }
    public IClientContentSource Source
    {
        get;
    }

    public static bool IsClientRepository(string root) => File.Exists(Path.Combine(root, "src", "OpenConquer.Client", "OpenConquer.Client.csproj"));

    public static StudioProject OpenWorkspace(string repository, CancellationToken token = default)
    {
        repository = AssetFileIO.RequireSafePath(repository);
        if (!IsClientRepository(repository))
        {
            throw new InvalidDataException("Select the OpenConquerClient source repository.");
        }
        string workspace = AssetFileIO.RequireSafePath(Path.Combine(repository, ".studio", "authoring"));
        if (File.Exists(Path.Combine(workspace, "project.ocstudio")))
        {
            return StudioProject.Open(workspace, token);
        }
        token.ThrowIfCancellationRequested();
        Directory.CreateDirectory(workspace);
        return StudioProject.Create(workspace, Path.GetFileName(repository));
    }

    public static (StudioProject Project, ClientProjectConnection Connection) OpenRepository(string repository, CancellationToken token = default)
    {
        repository = AssetFileIO.RequireSafePath(repository);
        string workspace = Path.Combine(repository, ".studio", "authoring");
        if (File.Exists(Path.Combine(workspace, "project.ocstudio")))
        {
            StudioProject project = StudioProject.Open(workspace, token);
            try
            {
                return (project, OpenAttached(workspace) ?? Attach(project, repository, Path.Combine(repository, "content", "retail-5517", "payload"), token));
            }
            catch
            {
                project.Dispose();
                throw;
            }
        }

        ClientProjectConnection connection = new(repository, Path.Combine(repository, "content", "retail-5517", "payload"));
        AssetFileIO.RequireSafePath(workspace);
        Directory.CreateDirectory(workspace);
        StudioProject created = StudioProject.Create(workspace, "OpenConquerClient");
        try
        {
            WriteSettings(created.Root, connection);
            return (created, connection);
        }
        catch
        {
            created.Dispose();
            throw;
        }
    }

    public static ClientProjectConnection Attach(StudioProject project, string repository, string contentRoot, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ClientProjectConnection connection = new(repository, contentRoot);
        _ = connection.Inventory(token);
        WriteSettings(project.Root, connection);
        return connection;
    }

    private static void WriteSettings(string workspace, ClientProjectConnection connection)
    {
        string directory = AssetFileIO.RequireSafePath(Path.Combine(workspace, ".user"));
        Directory.CreateDirectory(directory);
        ClientProjectSettings settings = new(1, Path.GetRelativePath(workspace, connection.Repository), Path.GetRelativePath(workspace, connection.ContentRoot));
        AssetFileIO.WriteAtomic(Path.Combine(directory, "client.json"), AssetFileIO.Serialize(settings), overwrite: true);
    }

    public static ClientProjectConnection? OpenAttached(string workspace)
    {
        string path = AssetFileIO.RequireSafePath(Path.Combine(workspace, ".user", "client.json"));
        if (!File.Exists(path))
        {
            return null;
        }

        ClientProjectSettings settings = AssetFileIO.Deserialize<ClientProjectSettings>(AssetFileIO.Read(path, 16 * 1024));
        if (settings.SchemaVersion != 1)
        {
            throw new InvalidDataException("Unsupported client connection settings. Reconnect the client content folder.");
        }

        return new ClientProjectConnection(Path.GetFullPath(settings.RepositoryPath, workspace), Path.GetFullPath(settings.ContentPath, workspace));
    }

    public ImmutableArray<ClientAssetEntry> Inventory(CancellationToken token = default)
    {
        List<ClientAssetEntry> entries = [];
        foreach (uint id in _maps.MapDataIds.Order())
        {
            _maps.TryGetTerrain(id, out GameMapCatalog.Terrain? map);
            entries.Add(new ClientAssetEntry("map", map!.Path, id, _loose.TryResolveFile(map.Path, out _)));
        }

        if (Source.TryOpenRead("ini/c3.wdb", ContentLookupMode.LooseOnly, out Stream? catalogProbe))
        {
            catalogProbe.Dispose();
            for (uint body = 1; body <= 4; body++)
            {
                entries.Add(new ClientAssetEntry("character", $"Character body {body}", Appearance: body));
            }
        }

        Stack<(string Directory, int Depth)> pending = new();
        pending.Push((ContentRoot, 0));
        int count = 0;
        while (pending.TryPop(out var folder))
        {
            token.ThrowIfCancellationRequested();
            foreach (string path in Directory.EnumerateFileSystemEntries(folder.Directory))
            {
                token.ThrowIfCancellationRequested();
                AssetFileIO.RequireSafePath(path);
                if (++count > 200_000 || folder.Depth > 32)
                {
                    throw new InvalidDataException("The client source inventory exceeds its 200,000-entry or 32-directory-depth limit.");
                }

                if (Directory.Exists(path))
                {
                    pending.Push((path, folder.Depth + 1));
                    continue;
                }

                string kind = Path.GetExtension(path).ToLowerInvariant() switch
                {
                    ".tga" or ".dds" => "texture",
                    ".ani" => "animation",
                    ".pul" => "puzzle",
                    ".c3" => "model",
                    ".wdf" => "archive",
                    ".wav" or ".mp3" or ".ogg" => "audio",
                    ".ini" or ".dat" or ".rgn" => "configuration",
                    ".scene" => "scene",
                    _ => "source",
                };
                entries.Add(new ClientAssetEntry(kind, Path.GetRelativePath(ContentRoot, path).Replace('\\', '/')));
            }
        }

        return [.. entries.OrderBy(a => a.Kind, StringComparer.Ordinal).ThenBy(a => a.Path, StringComparer.Ordinal).ThenBy(a => a.MapDataId)];
    }

    public OpenConquer.Content.Mesh.C3ModelData ReadModel(string path, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        byte[] bytes = ContentReader.ReadRequiredBytes(Source, path, ContentLookupMode.LooseThenPackage,
            OpenConquer.Content.Mesh.C3MeshFile.MaximumFileBytes);
        return OpenConquer.Content.Mesh.C3ModelImporter.File(Guid.NewGuid(), path, bytes);
    }

    public ImmutableArray<string> AnimationSections(string path) => [.. AniIndexFile.Load(Source, path, ContentLookupMode.LooseThenPackage).Sections.Select(s => s.Name)];

    public ImportedImageFrame ReadImage(string path, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        string format = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".tga" => "tga",
            ".dds" => "dds",
            _ => throw new InvalidDataException("The selected client image is not TGA/DDS."),
        };
        byte[] bytes = ContentReader.ReadRequiredBytes(Source, path, ContentLookupMode.LooseThenPackage, 16 * 1024 * 1024);
        (TextureData texture, _, string digest) = TextureImporter.Decode(bytes, format, token);
        string key = "conquer/source/" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(path.ToLowerInvariant())));
        return new ImportedImageFrame(Path.GetFileNameWithoutExtension(path), texture, format, digest, key);
    }

    public ImmutableArray<ImportedImageFrame> ReadAnimation(string path, string sectionName, CancellationToken token = default)
    {
        AniIndexSection section = AniIndexFile.Load(Source, path, ContentLookupMode.LooseThenPackage).GetRequiredSection(sectionName);
        if (section.Name != sectionName || section.FrameCount is < 1 or > ProjectDocument.MaximumAssets)
        {
            throw new InvalidDataException("Select an exact ANI section with 1–64 frames. Larger clips exceed this project's current texture budget.");
        }

        List<ImportedImageFrame> frames = [];
        long bytes = 0;
        foreach (string framePath in section.FramePaths)
        {
            ImportedImageFrame frame = ReadImage(framePath, token);
            bytes += frame.Texture.Pixels.Length;
            if (bytes > ProjectDocument.MaximumTotalPixelBytes)
            {
                throw new InvalidDataException("ANI frame pixels exceed the 64 MiB project budget.");
            }

            frames.Add(frame);
        }

        return [.. frames];
    }

    public TerrainImport ReadTerrainWithSource(uint id, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!_maps.TryGetTerrain(id, out GameMapCatalog.Terrain? terrain))
        {
            throw new InvalidDataException($"Map data ID {id} is absent from the source map catalog.");
        }

        byte[] archive = ContentReader.ReadRequiredBytes(Source, terrain.Path,
            ContentLookupMode.LooseOnly, 128 * 1024);
        byte[] decoded = GameMapArchive.Decode(archive, terrain.Path);
        GameMapCellGrid grid = GameMapCellGrid.Parse(decoded);
        GameMapSceneSections scene = GameMapSceneSections.Parse(decoded, grid.BytesConsumed, grid.HeaderField0);
        TerrainImport extracted = ClientTerrainImporter.Read(id, terrain.Path, terrain.CompanionValue,
            grid, scene, token);
        ImmutableArray<SourcePuzzleReference> references = SourcePuzzleReferences.Collect(grid, scene);
        ImmutableArray<SourcePuzzleCapture>.Builder captures = ImmutableArray.CreateBuilder<SourcePuzzleCapture>(references.Length);
        for (int i = 0; i < references.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            SourcePuzzleReference reference = references[i];
            string path = reference.Path;
            if (!Source.TryOpenRead(path, ContentLookupMode.LooseOnly, out Stream? stream))
            {
                if (reference.Required)
                {
                    throw new InvalidDataException($"Required map puzzle '{path}' is missing.");
                }
                captures.Add(new SourcePuzzleCapture(path, null));
                continue;
            }

            using (stream)
            {
                byte[] puzzle = ContentReader.ReadBytes(stream, path, SourcePuzzleInput.MaximumBytes);
                _ = GameMapPuzzleBitmap.Parse(puzzle);
                captures.Add(new SourcePuzzleCapture(path, puzzle));
            }
        }

        return extracted with
        {
            DecodedSource = decoded,
            PuzzleFiles = captures.MoveToImmutable()
        };
    }

    public GameMapContent ReadMap(uint id) => GameMapContent.Load(Source, _maps, id);

    public string DescribeSource(ClientAssetEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        string path = AssetFileIO.RequireSafePath(_loose.ResolveRequiredFile(entry.Path));
        return $"{entry.Path}\n{new FileInfo(path).Length} bytes\nKind: {entry.Kind}\nSource metadata only. This resource kind is not yet editable or converted by Studio.";
    }

    public GameMapPuzzleBitmap ReadPuzzle(string path) => GameMapPuzzleBitmap.Parse(
        ContentReader.ReadRequiredBytes(Source, path, ContentLookupMode.LooseOnly, 16 * 1024 * 1024));

    public static string DescribeMap(GameMapContent map) => string.Create(CultureInfo.InvariantCulture,
        $"Map {map.MapDataId} · {map.CellGrid.Width} × {map.CellGrid.Height} cells\n{map.ArchivePath}\nPuzzle: {map.CellGrid.PuzzlePath}\nExits: {map.Scene.ExitMarkers.Count} · Scenery: {map.Scene.Scenery.Count} · Overlay groups: {map.Scene.PositionedOverlays.Count}");
}
