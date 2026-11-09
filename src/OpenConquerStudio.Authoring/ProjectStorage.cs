using System.Collections.Immutable;
using System.Text.Json;
using OpenConquer.Content.Map;
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

    internal static (ProjectDocument Document, Dictionary<string, TextureData> Pixels, string Digest, Dictionary<string, byte[]> Models,
        Dictionary<string, byte[]> Terrains, Dictionary<string, byte[]> SourceInputs) Read(string root, CancellationToken token)
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
        return (document, ReadPixels(root, document, token), ProjectFiles.Hash(bytes), payloads,
            ReadTerrains(root, document, token), ReadSourceInputs(root, document, token));
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
        if (document.Asset is null || document.SchemaVersion != TerrainResourceVersion(document.Asset))
        {
            throw new InvalidDataException("Unsupported or inconsistent terrain resource version.");
        }

        return document.Asset;
    }

    private static int TerrainResourceVersion(TerrainAsset terrain) =>
        terrain.Source is null ? 1 : terrain.Source.SnapshotDigest is null ? 2 :
            terrain.Source.Puzzles.IsEmpty ? 3 : !terrain.Source.Scenes.IsEmpty || !terrain.Source.Frames.IsEmpty ? 6 :
            terrain.Source.AnimationIndexes.IsEmpty ? 4 : 5;

    private static Dictionary<string, byte[]> ReadSourceInputs(string root, ProjectDocument document, CancellationToken token)
    {
        Dictionary<string, byte[]> inputs = new(StringComparer.Ordinal);
        foreach (TerrainAsset terrain in document.Terrains)
        {
            token.ThrowIfCancellationRequested();
            if (terrain.Source is not { } source)
            {
                continue;
            }

            if (source.SnapshotDigest is string mapDigest && !inputs.ContainsKey(mapDigest))
            {
                byte[] decoded = ProjectFiles.Read(MapSourcePath(root, mapDigest), TerrainSource.MaximumDecodedSourceBytes);
                if (decoded.Length != source.SnapshotLength || ProjectFiles.Hash(decoded) != mapDigest)
                {
                    throw new InvalidDataException("Owned map source does not match its immutable revision.");
                }
                inputs.Add(mapDigest, decoded);
            }
            if (source.SnapshotDigest is string storedMap)
            {
                _ = SourceMapInventory.Read(terrain.MapDataId, source, inputs[storedMap]);
            }

            foreach (SourcePuzzleInput puzzle in source.Puzzles)
            {
                token.ThrowIfCancellationRequested();
                if (puzzle.Digest is not string digest)
                {
                    continue;
                }

                if (!inputs.TryGetValue(digest, out byte[]? bytes))
                {
                    bytes = ProjectFiles.Read(PuzzleSourcePath(root, digest), SourcePuzzleInput.MaximumBytes);
                    if (bytes.Length != puzzle.ByteLength || ProjectFiles.Hash(bytes) != digest)
                    {
                        throw new InvalidDataException("Owned puzzle does not match its immutable revision.");
                    }
                    inputs.Add(digest, bytes);
                }

                SourcePuzzleInput decoded = SourcePuzzleInput.FromBytes(puzzle.Path, bytes);
                if (decoded != puzzle)
                {
                    throw new InvalidDataException("Owned puzzle metadata differs from its decoded input.");
                }
            }
            ImmutableArray<SourceSceneCapture> scenes = default;
            if (!source.Scenes.IsEmpty || !source.Frames.IsEmpty)
            {
                GameMapCellGrid grid = GameMapCellGrid.Parse(inputs[source.SnapshotDigest!]);
                GameMapSceneSections parsed = GameMapSceneSections.Parse(inputs[source.SnapshotDigest!],
                    grid.BytesConsumed, grid.HeaderField0);
                ImmutableArray<string> requiredScenes = SourceSceneDependencies.Collect(parsed);
                if (requiredScenes.Length != source.Scenes.Length)
                {
                    throw new InvalidDataException("Captured scenery files do not cover the original map references.");
                }
                ImmutableArray<SourceSceneCapture>.Builder captures =
                    ImmutableArray.CreateBuilder<SourceSceneCapture>(requiredScenes.Length);
                for (int i = 0; i < requiredScenes.Length; i++)
                {
                    token.ThrowIfCancellationRequested();
                    SourceSceneInput recorded = source.Scenes[i];
                    if (!string.Equals(recorded.Path, requiredScenes[i], StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("Scenery file order differs from the original map.");
                    }
                    if (!inputs.TryGetValue(recorded.Digest, out byte[]? bytes))
                    {
                        bytes = ProjectFiles.Read(SceneSourcePath(root, recorded.Digest),
                            SourceSceneInput.MaximumBytes);
                        if (bytes.Length != recorded.ByteLength || ProjectFiles.Hash(bytes) != recorded.Digest)
                        {
                            throw new InvalidDataException("Stored scenery source failed integrity validation.");
                        }
                        inputs.Add(recorded.Digest, bytes);
                    }
                    SourceSceneInput parsedScene = SourceSceneDependencies.Read(recorded.Path, bytes);
                    if (!SourceSceneDependencies.Equivalent([recorded], [parsedScene]))
                    {
                        throw new InvalidDataException("Scenery object metadata differs from its stored bytes.");
                    }
                    captures.Add(new SourceSceneCapture(recorded.Path, bytes));
                }
                scenes = captures.MoveToImmutable();
            }
            if (!source.AnimationIndexes.IsEmpty)
            {
                GameMapCellGrid grid = GameMapCellGrid.Parse(inputs[source.SnapshotDigest!]);
                GameMapSceneSections scene = GameMapSceneSections.Parse(inputs[source.SnapshotDigest!],
                    grid.BytesConsumed, grid.HeaderField0);
                ImmutableArray<SourcePuzzleCapture> puzzles = [.. source.Puzzles.Select(puzzle =>
                    new SourcePuzzleCapture(puzzle.Path, puzzle.Digest is null ? null : inputs[puzzle.Digest]))];
                ImmutableArray<SourceAnimationRequirement> required =
                    SourceAnimationDependencies.Collect(scene, puzzles, scenes);
                if (required.Length != source.AnimationIndexes.Length)
                {
                    throw new InvalidDataException("Stored animation indexes do not cover source-map dependencies.");
                }
                for (int i = 0; i < required.Length; i++)
                {
                    token.ThrowIfCancellationRequested();
                    SourceAnimationIndexInput recorded = source.AnimationIndexes[i];
                    if (!string.Equals(recorded.Path, required[i].Path, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("Animation index order differs from source-map dependencies.");
                    }
                    if (!inputs.TryGetValue(recorded.Digest, out byte[]? bytes))
                    {
                        bytes = ProjectFiles.Read(AnimationIndexPath(root, recorded.Digest),
                            SourceAnimationIndexInput.MaximumBytes);
                        if (bytes.Length != recorded.ByteLength || ProjectFiles.Hash(bytes) != recorded.Digest)
                        {
                            throw new InvalidDataException("Stored animation index failed its digest or length check.");
                        }
                        inputs.Add(recorded.Digest, bytes);
                    }
                    SourceAnimationIndexInput parsed = SourceAnimationDependencies.Read(required[i], bytes);
                    if (!SourceAnimationDependencies.Equivalent([recorded], [parsed]))
                    {
                        throw new InvalidDataException("Captured animation index differs from its required sections.");
                    }
                }
            }
            if (!source.Frames.IsEmpty)
            {
                ImmutableArray<string> requiredFrames = SourceFrameDependencies.Collect(source.AnimationIndexes);
                if (requiredFrames.Length != source.Frames.Length)
                {
                    throw new InvalidDataException("Captured frame images do not cover the ANI references.");
                }
                for (int i = 0; i < requiredFrames.Length; i++)
                {
                    token.ThrowIfCancellationRequested();
                    SourceFrameInput recorded = source.Frames[i];
                    if (!string.Equals(recorded.Path, requiredFrames[i], StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("Captured frame order differs from its ANI references.");
                    }
                    if (!inputs.TryGetValue(recorded.Digest, out byte[]? bytes))
                    {
                        bytes = ProjectFiles.Read(FrameSourcePath(root, recorded.Digest),
                            SourceFrameInput.MaximumBytes);
                        if (bytes.Length != recorded.ByteLength || ProjectFiles.Hash(bytes) != recorded.Digest)
                        {
                            throw new InvalidDataException("Captured image frame failed its digest or byte-length check.");
                        }
                        inputs.Add(recorded.Digest, bytes);
                    }
                    if (SourceFrameDependencies.Read(recorded.Path, bytes, token) != recorded)
                    {
                        throw new InvalidDataException("Captured frame image metadata differs from its original bytes.");
                    }
                }
            }
        }
        return inputs;
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

    internal static string Save(string root, ProjectDocument document, Dictionary<string, TextureData> pixels, Dictionary<string, byte[]> models,
        Dictionary<string, byte[]> terrains, Dictionary<string, byte[]> sourceInputs, string? savedDigest, CancellationToken token)
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

        foreach (string digest in document.Terrains.Select(a => a.Source?.SnapshotDigest).OfType<string>().Distinct())
        {
            token.ThrowIfCancellationRequested();
            WriteImmutable(MapSourcePath(root, digest), sourceInputs[digest], TerrainSource.MaximumDecodedSourceBytes);
        }
        foreach (string digest in document.Terrains.SelectMany(a => a.Source?.Puzzles ?? [])
            .Select(puzzle => puzzle.Digest).OfType<string>().Distinct())
        {
            token.ThrowIfCancellationRequested();
            WriteImmutable(PuzzleSourcePath(root, digest), sourceInputs[digest], SourcePuzzleInput.MaximumBytes);
        }
        foreach (string digest in document.Terrains.SelectMany(a => a.Source?.AnimationIndexes ?? [])
            .Select(index => index.Digest).Distinct())
        {
            token.ThrowIfCancellationRequested();
            WriteImmutable(AnimationIndexPath(root, digest), sourceInputs[digest],
                SourceAnimationIndexInput.MaximumBytes);
        }
        foreach (string digest in document.Terrains.SelectMany(a => a.Source?.Scenes ?? [])
            .Select(scene => scene.Digest).Distinct())
        {
            token.ThrowIfCancellationRequested();
            WriteImmutable(SceneSourcePath(root, digest), sourceInputs[digest],
                SourceSceneInput.MaximumBytes);
        }
        foreach (string digest in document.Terrains.SelectMany(a => a.Source?.Frames ?? [])
            .Select(frame => frame.Digest).Distinct())
        {
            token.ThrowIfCancellationRequested();
            WriteImmutable(FrameSourcePath(root, digest), sourceInputs[digest],
                SourceFrameInput.MaximumBytes);
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
        ProjectFiles.Serialize(new ResourceDocument<TerrainAsset>(TerrainResourceVersion(terrain), terrain));

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

    private static string MapSourcePath(string root, string digest) => Path.Combine(root, "Inputs", digest + ".ocmapsource");

    private static string PuzzleSourcePath(string root, string digest) => Path.Combine(root, "Inputs", digest + ".ocpuzzle");

    private static string AnimationIndexPath(string root, string digest) => Path.Combine(root, "Inputs", digest + ".ocani");

    private static string SceneSourcePath(string root, string digest) => Path.Combine(root, "Inputs", digest + ".ocscene");

    private static string FrameSourcePath(string root, string digest) => Path.Combine(root, "Inputs", digest + ".ocframe");
}
