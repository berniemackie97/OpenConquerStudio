using System.Collections.Immutable;
using System.Text.Json;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio.Authoring;

internal sealed record ResourceRevision(Guid Id, string Kind, string Sha256);
internal sealed record ProjectIndex(int SchemaVersion, Guid Id, string Name, ImmutableArray<ResourceRevision> Resources);
internal sealed record ResourceDocument<T>(int SchemaVersion, T Asset);
internal sealed record LegacyProject(int SchemaVersion, Guid Id, string Name, ImmutableArray<TextureAsset> Assets);

/// <summary>Immutable revisions are written first; the project index is the sole save commit point.</summary>
internal static class ProjectStorage
{
    internal const string ManifestName = "project.ocstudio";
    private const int MaximumResourceBytes = 2 * 1024 * 1024;
    private const int MaximumMetadataBytes = 8 * 1024 * 1024;

    internal static (ProjectDocument Document, Dictionary<string, TextureData> Pixels, string Digest, Dictionary<string, byte[]> Models, Dictionary<string, byte[]> Terrains) Read(string root, CancellationToken token)
    {
        byte[] bytes = ProjectFiles.Read(Path.Combine(root, ProjectStorage.ManifestName), ProjectFiles.MaximumManifestBytes);
        using (JsonDocument json = JsonDocument.Parse(bytes))
        {
            if (json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("schemaVersion", out JsonElement version) &&
                version.TryGetInt32(out int value) && value == 1)
            {
                throw new InvalidDataException("Version 1 projects require migration. Use 'migrate <source> <empty-destination>'; the source is preserved.");
            }
        }

        ProjectIndex index = ProjectFiles.Deserialize<ProjectIndex>(bytes);
        if (index.SchemaVersion != ProjectDocument.CurrentVersion || index.Id == Guid.Empty || index.Resources.IsDefault ||
            index.Resources.Length > ProjectDocument.MaximumResources)
        {
            throw new InvalidDataException("Unsupported project index version, identity, or resource count.");
        }

        List<ModelAsset> models = [];
        List<TerrainAsset> terrains = [];
        List<WorldAsset> worlds = [];
        List<TextureAsset> textures = [];
        List<AtlasAsset> atlases = [];
        List<AnimationAsset> animations = [];
        Guid previous = Guid.Empty;
        int total = 0;
        foreach (ResourceRevision revision in index.Resources)
        {
            token.ThrowIfCancellationRequested();
            if (revision is null || revision.Id == Guid.Empty || revision.Id.CompareTo(previous) <= 0)
            {
                throw new InvalidDataException("Resource revisions must have unique, sorted identities.");
            }

            previous = revision.Id;
            ProjectDocument.ValidateDigest(revision.Sha256);
            byte[] assetBytes = ProjectFiles.Read(RevisionPath(root, revision), MaximumResourceBytes);
            total += assetBytes.Length;
            if (total > MaximumMetadataBytes || ProjectFiles.Hash(assetBytes) != revision.Sha256)
            {
                throw new InvalidDataException("Resource metadata exceeds its project budget or failed its digest check.");
            }

            Guid id;
            switch (revision.Kind)
            {
                case "model":
                    ModelAsset model = Decode<ModelAsset>(assetBytes);
                    models.Add(model);
                    id = model.Id;
                    break;
                case "terrain":
                    TerrainAsset terrain = DecodeTerrain(assetBytes);
                    terrains.Add(terrain);
                    id = terrain.Id;
                    break;
                case "world":
                    WorldAsset world = Decode<WorldAsset>(assetBytes);
                    worlds.Add(world);
                    id = world.Id;
                    break;
                case "texture":
                    TextureAsset texture = Decode<TextureAsset>(assetBytes);
                    textures.Add(texture);
                    id = texture.Id;
                    break;
                case "atlas":
                    AtlasAsset atlas = Decode<AtlasAsset>(assetBytes);
                    atlases.Add(atlas);
                    id = atlas.Id;
                    break;
                case "animation":
                    AnimationAsset animation = Decode<AnimationAsset>(assetBytes);
                    animations.Add(animation);
                    id = animation.Id;
                    break;
                default:
                    throw new InvalidDataException($"Unsupported resource kind '{revision.Kind}'.");
            }

            if (id != revision.Id)
            {
                throw new InvalidDataException("Resource identity does not match its project index.");
            }
        }

        ProjectDocument document = new(index.SchemaVersion, index.Id, index.Name, [.. textures], [.. atlases], [.. animations])
        {
            Models = [.. models],
            Terrains = [.. terrains],
            Worlds = [.. worlds]
        };
        document.Validate();
        Dictionary<string, byte[]> payloads = [];
        foreach (ModelAsset model in models)
        {
            token.ThrowIfCancellationRequested();
            byte[] data = ProjectFiles.Read(ModelPath(root, model.DataDigest), OpenConquer.Content.Mesh.C3ModelCodec.MaximumLength);
            if (data.Length != model.ByteLength || ProjectFiles.Hash(data) != model.DataDigest ||
                OpenConquer.Content.Mesh.C3ModelCodec.Read(data).Definition.Id != model.Id)
            {
                throw new InvalidDataException("Model data does not match its revision.");
            }
            payloads.TryAdd(model.DataDigest, data);
        }
        return (document, ReadPixels(root, document, token), ProjectFiles.Hash(bytes), payloads, ReadTerrains(root, document, token));
    }

    private static T Decode<T>(byte[] bytes) where T : class
    {
        ResourceDocument<T> resource = ProjectFiles.Deserialize<ResourceDocument<T>>(bytes);
        if (resource.SchemaVersion != 1 || resource.Asset is null)
        {
            throw new InvalidDataException("Unsupported or empty resource document.");
        }

        return resource.Asset;
    }

    private static TerrainAsset DecodeTerrain(byte[] bytes)
    {
        ResourceDocument<TerrainAsset> document = ProjectFiles.Deserialize<ResourceDocument<TerrainAsset>>(bytes);
        if (document.Asset is null || document.SchemaVersion != (document.Asset.Source is null ? 1 : 2))
        {
            throw new InvalidDataException("Unsupported or inconsistent terrain resource version.");
        }

        return document.Asset;
    }

    internal static Dictionary<string, TextureData> ReadPixels(string root, ProjectDocument document, CancellationToken token)
    {
        Dictionary<string, TextureData> pixels = new(StringComparer.Ordinal);
        foreach (TextureAsset asset in document.Assets)
        {
            token.ThrowIfCancellationRequested();
            if (!pixels.TryGetValue(asset.PixelDigest, out TextureData? texture))
            {
                byte[] data = ProjectFiles.Read(PixelPath(root, asset.PixelDigest), TextureData.MaximumPixelBytes);
                if (ProjectFiles.Hash(data) != asset.PixelDigest)
                {
                    throw new InvalidDataException($"Pixel data for '{asset.Name}' failed its SHA-256 check.");
                }

                texture = new TextureData(asset.Width, asset.Height, data);
                pixels.Add(asset.PixelDigest, texture);
            }

            if (texture.Pixels.Length != TextureData.GetByteLength(asset.Width, asset.Height))
            {
                throw new InvalidDataException("Pixel length does not match its texture dimensions.");
            }
        }

        return pixels;
    }

    private static Dictionary<string, byte[]> ReadTerrains(string root, ProjectDocument document, CancellationToken token)
    {
        Dictionary<string, byte[]> cells = new(StringComparer.Ordinal);
        foreach (TerrainAsset terrain in document.Terrains)
        {
            token.ThrowIfCancellationRequested();
            if (!cells.TryGetValue(terrain.CellDigest, out byte[]? data))
            {
                data = ProjectFiles.Read(TerrainPath(root, terrain.CellDigest), TerrainGrid.MaximumBytes);
                if (ProjectFiles.Hash(data) != terrain.CellDigest)
                {
                    throw new InvalidDataException($"Terrain '{terrain.Name}' failed its cell payload integrity check.");
                }
                cells.Add(terrain.CellDigest, data);
            }

            if (data.Length != TerrainGrid.ByteLength(terrain.Width, terrain.Height))
            {
                throw new InvalidDataException($"Terrain '{terrain.Name}' cell payload length does not match its dimensions.");
            }
        }

        return cells;
    }

    internal static string Save(string root, ProjectDocument document, Dictionary<string, TextureData> pixels, Dictionary<string, byte[]> models, Dictionary<string, byte[]> terrains, string? savedDigest, CancellationToken token)
    {
        document.Validate();
        string manifest = Path.Combine(root, ProjectStorage.ManifestName);
        CheckUnchanged(manifest, savedDigest);
        foreach (TextureAsset asset in document.Assets)
        {
            token.ThrowIfCancellationRequested();
            WriteImmutable(PixelPath(root, asset.PixelDigest), pixels[asset.PixelDigest].Pixels.Span, TextureData.MaximumPixelBytes);
        }

        foreach (ModelAsset model in document.Models)
        {
            token.ThrowIfCancellationRequested();
            WriteImmutable(ModelPath(root, model.DataDigest), models[model.DataDigest], OpenConquer.Content.Mesh.C3ModelCodec.MaximumLength);
        }
        foreach (TerrainAsset terrain in document.Terrains)
        {
            token.ThrowIfCancellationRequested();
            WriteImmutable(TerrainPath(root, terrain.CellDigest), terrains[terrain.CellDigest], TerrainGrid.MaximumBytes);
        }

        List<ResourceRevision> revisions = [];
        int metadataBytes = 0;
        foreach (ResourceInfo info in document.Resources)
        {
            token.ThrowIfCancellationRequested();
            byte[] bytes = info.Kind switch
            {
                "model" => ProjectFiles.Serialize(new ResourceDocument<ModelAsset>(1, document.Models.Single(a => a.Id == info.Id))),
                "terrain" => SerializeTerrain(document.Terrains.Single(a => a.Id == info.Id)),
                "world" => ProjectFiles.Serialize(new ResourceDocument<WorldAsset>(1, document.Worlds.Single(a => a.Id == info.Id))),
                "texture" => ProjectFiles.Serialize(new ResourceDocument<TextureAsset>(1, document.Assets.Single(a => a.Id == info.Id))),
                "atlas" => ProjectFiles.Serialize(new ResourceDocument<AtlasAsset>(1, document.Atlases.Single(a => a.Id == info.Id))),
                "animation" => ProjectFiles.Serialize(new ResourceDocument<AnimationAsset>(1, document.Animations.Single(a => a.Id == info.Id))),
                _ => throw new InvalidOperationException("Unsupported resource kind."),
            };
            metadataBytes += bytes.Length;
            if (bytes.Length > MaximumResourceBytes || metadataBytes > MaximumMetadataBytes)
            {
                throw new InvalidDataException("Resource metadata exceeds its save budget.");
            }

            ResourceRevision revision = new(info.Id, info.Kind, ProjectFiles.Hash(bytes));
            WriteImmutable(RevisionPath(root, revision), bytes, MaximumResourceBytes);
            revisions.Add(revision);
        }

        byte[] index = ProjectFiles.Serialize(new ProjectIndex(ProjectDocument.CurrentVersion, document.Id, document.Name, [.. revisions]));
        token.ThrowIfCancellationRequested();
        CheckUnchanged(manifest, savedDigest);
        ProjectFiles.WriteAtomic(manifest, index, overwrite: savedDigest is not null);
        return ProjectFiles.Hash(index);
    }

    private static byte[] SerializeTerrain(TerrainAsset terrain) =>
        ProjectFiles.Serialize(new ResourceDocument<TerrainAsset>(terrain.Source is null ? 1 : 2, terrain));

    private static void CheckUnchanged(string path, string? digest)
    {
        if (digest is not null && ProjectFiles.Hash(ProjectFiles.Read(path, ProjectFiles.MaximumManifestBytes)) != digest)
        {
            throw new IOException("The project index changed outside this session. Reopen it before saving.");
        }
    }

    private static void WriteImmutable(string path, ReadOnlySpan<byte> bytes, int maximum)
    {
        ProjectFiles.RequireSafePath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            if (!ProjectFiles.Read(path, maximum).AsSpan().SequenceEqual(bytes))
            {
                throw new InvalidDataException("An existing immutable project revision is corrupt; save was stopped.");
            }
        }
        else
        {
            ProjectFiles.WriteAtomic(path, bytes, overwrite: false);
        }
    }

    private static string RevisionPath(string root, ResourceRevision revision) =>
        Path.Combine(root, "Assets", revision.Id.ToString("N"), revision.Sha256 + ".ocasset.json");

    private static string ModelPath(string root, string digest) => Path.Combine(root, "Inputs", digest + ".ocmodel");

    private static string PixelPath(string root, string digest) => Path.Combine(root, "Inputs", digest + ".rgba");

    private static string TerrainPath(string root, string digest) => Path.Combine(root, "Inputs", digest + ".octerrain");
}
