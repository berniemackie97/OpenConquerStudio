using System.Collections.Immutable;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio.Authoring;

public sealed record TextureAsset(Guid Id, string Name, int Width, int Height, string PixelDigest,
    string SourceFormat, string SourceDigest, bool FlipHorizontal, int ClockwiseQuarterTurns, string? SourceKey = null);

public sealed record ModelAsset(Guid Id, string Name, string DataDigest, int ByteLength);

public readonly record struct TerrainExitMarker(Guid Id, int X, int Y, uint PasswayIndex);

public sealed record TerrainAsset(Guid Id, string Name, uint MapDataId, int Width, int Height, string CellDigest)
{
    public ImmutableArray<TerrainExitMarker> Exits { get; init; } = [];
    public TerrainSource? Source
    {
        get; init;
    }
}

public sealed record AtlasAsset(Guid Id, string Name, Guid TextureId, ImmutableArray<SpriteRegion> Sprites);
public sealed record AnimationAsset(Guid Id, string Name, int TicksPerSecond, bool Repeat, ImmutableArray<AnimationFrame> Frames);
public sealed record ResourceInfo(Guid Id, string Name, string Kind)
{
    public override string ToString() => $"{Name} ({Kind})";
}

public sealed partial record ProjectDocument(int SchemaVersion, Guid Id, string Name, ImmutableArray<TextureAsset> Assets,
    ImmutableArray<AtlasAsset> Atlases, ImmutableArray<AnimationAsset> Animations)
{
    public ImmutableArray<ModelAsset> Models { get; init; } = [];
    public ImmutableArray<TerrainAsset> Terrains { get; init; } = [];
    public ImmutableArray<WorldAsset> Worlds { get; init; } = [];
    public const int MaximumTotalTerrainCells = 262_144;
    public const int MaximumTotalSourceMapBytes = 64 * 1024 * 1024;
    public const int MaximumTotalPuzzleBytes = 16 * 1024 * 1024;
    public const int MaximumTotalAnimationIndexBytes = 32 * 1024 * 1024;
    public const int MaximumTotalSceneBytes = 16 * 1024 * 1024;
    public const int MaximumTotalFrameBytes = 64 * 1024 * 1024;
    public const int MaximumTerrainExitMarkers = 4_096;
    public const int MaximumTotalTerrainExitMarkers = 8_192;
    public const int MaximumModelBytes = 128 * 1024 * 1024;
    public const int CurrentVersion = 2;
    public const int MaximumAssets = 64;
    public const int MaximumResources = 256;
    public const int MaximumTotalPixelBytes = 64 * 1024 * 1024;

    public IEnumerable<ResourceInfo> Resources => Assets.Select(a => new ResourceInfo(a.Id, a.Name, "texture"))
        .Concat(Atlases.Select(a => new ResourceInfo(a.Id, a.Name, "atlas")))
        .Concat(Models.Select(a => new ResourceInfo(a.Id, a.Name, "model")))
        .Concat(Terrains.Select(a => new ResourceInfo(a.Id, a.Name, "terrain")))
        .Concat(Worlds.Select(a => new ResourceInfo(a.Id, a.Name, "world")))
        .Concat(Animations.Select(a => new ResourceInfo(a.Id, a.Name, "animation"))).OrderBy(a => a.Id);

    public void Validate()
    {
        if (SchemaVersion != CurrentVersion)
        {
            throw new InvalidDataException($"Project version {SchemaVersion} is unsupported; expected {CurrentVersion}. Use migration to copy version 1 projects.");
        }

        ValidateName(Name);
        if (Id == Guid.Empty || Assets.IsDefault || Atlases.IsDefault || Animations.IsDefault || Assets.Length > MaximumAssets ||
            Models.IsDefault || Terrains.IsDefault || Worlds.IsDefault ||
            Assets.Length + Atlases.Length + Animations.Length + Models.Length + Terrains.Length + Worlds.Length > MaximumResources)
        {
            throw new InvalidDataException("Project identity or asset count is invalid.");
        }

        HashSet<Guid> ids = [];
        long total = 0;
        foreach (TextureAsset asset in Assets)
        {
            if (asset is null || asset.Id == Guid.Empty || !ids.Add(asset.Id))
            {
                throw new InvalidDataException("Asset identities must be nonempty and unique.");
            }

            ValidateName(asset.Name);
            ValidateDigest(asset.PixelDigest);
            ValidateDigest(asset.SourceDigest);
            if (asset.SourceKey is not null && (asset.SourceKey.Length is < 1 or > 256 ||
                asset.SourceKey.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '/' or '-' or '_'))))
            {
                throw new InvalidDataException("The image source key is invalid.");
            }
            total += TextureData.GetByteLength(asset.Width, asset.Height);
            if (asset.ClockwiseQuarterTurns is < 0 or > 3 || asset.SourceFormat is not ("tga" or "dds" or "rgba" or "created"))
            {
                throw new InvalidDataException($"Asset '{asset.Name}' has unsupported import or edit settings.");
            }
        }

        if (total > MaximumTotalPixelBytes)
        {
            throw new InvalidDataException("The project exceeds its 64 MiB aggregate pixel budget.");
        }

        long modelBytes = 0;
        foreach (ModelAsset model in Models)
        {
            if (model is null || model.Id == Guid.Empty || !ids.Add(model.Id) || model.ByteLength < 1 ||
                model.ByteLength > OpenConquer.Content.Mesh.C3ModelCodec.MaximumLength)
            {
                throw new InvalidDataException("Invalid model identity or payload length.");
            }
            ValidateName(model.Name);
            ValidateDigest(model.DataDigest);
            modelBytes += model.ByteLength;
        }
        if (modelBytes > MaximumModelBytes)
        {
            throw new InvalidDataException("Model assets exceed the 128 MiB project budget.");
        }

        HashSet<Guid> promotedTextures = [];
        long terrainCells = 0;
        int terrainExitMarkers = 0;
        HashSet<uint> mapDataIds = [];
        HashSet<string> sourceDigests = new(StringComparer.Ordinal);
        HashSet<string> puzzleDigests = new(StringComparer.Ordinal);
        HashSet<string> animationDigests = new(StringComparer.Ordinal);
        HashSet<string> sceneDigests = new(StringComparer.Ordinal);
        HashSet<string> frameDigests = new(StringComparer.Ordinal);
        long sourceBytes = 0;
        long puzzleBytes = 0;
        long animationBytes = 0;
        long sceneBytes = 0;
        long frameBytes = 0;
        foreach (TerrainAsset terrain in Terrains)
        {
            if (terrain is null || terrain.Id == Guid.Empty || !ids.Add(terrain.Id) || terrain.MapDataId == 0 ||
                !mapDataIds.Add(terrain.MapDataId))
            {
                throw new InvalidDataException("Terrain identities and map data IDs must be nonempty and unique.");
            }

            ValidateName(terrain.Name);
            ValidateDigest(terrain.CellDigest);
            terrain.Source?.Validate();
            if (terrain.Source is { } source)
            {
                foreach (SourcePuzzleInput puzzle in source.Puzzles)
                {
                    if (puzzle.Digest is string puzzleDigest && puzzleDigests.Add(puzzleDigest))
                    {
                        puzzleBytes += puzzle.ByteLength;
                        if (puzzleBytes > MaximumTotalPuzzleBytes)
                        {
                            throw new InvalidDataException("Project-owned puzzle inputs exceed their total size budget.");
                        }
                    }
                }
                foreach (SourceAnimationIndexInput index in source.AnimationIndexes)
                {
                    if (animationDigests.Add(index.Digest))
                    {
                        animationBytes += index.ByteLength;
                        if (animationBytes > MaximumTotalAnimationIndexBytes)
                        {
                            throw new InvalidDataException("Project-owned animation indexes exceed their total size budget.");
                        }
                    }
                }
                foreach (SourceSceneInput scene in source.Scenes)
                {
                    if (sceneDigests.Add(scene.Digest))
                    {
                        sceneBytes += scene.ByteLength;
                        if (sceneBytes > MaximumTotalSceneBytes)
                        {
                            throw new InvalidDataException("Project-owned scenery files exceed their total size budget.");
                        }
                    }
                }
                foreach (SourceFrameInput frame in source.Frames)
                {
                    if (frame.TextureId is Guid id)
                    {
                        if (!promotedTextures.Add(id))
                        {
                            throw new InvalidDataException("Multiple source frames share a promoted texture identity.");
                        }
                        TextureAsset? texture = Assets.FirstOrDefault(asset => asset.Id == id);
                        if (texture is null || texture.SourceDigest != frame.Digest ||
                            texture.SourceFormat != frame.Format ||
                            texture.Width != frame.Width || texture.Height != frame.Height)
                        {
                            throw new InvalidDataException($"Source frame '{frame.Path}' has an invalid promoted texture binding.");
                        }
                    }
                    if (frameDigests.Add(frame.Digest))
                    {
                        frameBytes += frame.ByteLength;
                        if (frameBytes > MaximumTotalFrameBytes)
                        {
                            throw new InvalidDataException("Project-owned image frames exceed their total size budget.");
                        }
                    }
                }
            }
            if (terrain.Source?.SnapshotDigest is string digest && sourceDigests.Add(digest))
            {
                sourceBytes += terrain.Source.SnapshotLength;
                if (sourceBytes > MaximumTotalSourceMapBytes)
                {
                    throw new InvalidDataException("Project-owned source maps exceed their total size budget.");
                }
            }
            if (terrain.Exits.IsDefault || terrain.Exits.Length > MaximumTerrainExitMarkers)
            {
                throw new InvalidDataException("Terrain exit marker count is invalid.");
            }
            terrainExitMarkers += terrain.Exits.Length;
            if (terrainExitMarkers > MaximumTotalTerrainExitMarkers)
            {
                throw new InvalidDataException("The project exceeds its terrain exit marker budget.");
            }
            foreach (TerrainExitMarker marker in terrain.Exits)
            {
                if (marker.Id == Guid.Empty || !ids.Add(marker.Id))
                {
                    throw new InvalidDataException("Terrain exit marker identities must be nonempty and unique.");
                }
            }

            terrainCells += TerrainGrid.CellCount(terrain.Width, terrain.Height);
            if (terrainCells > MaximumTotalTerrainCells)
            {
                throw new InvalidDataException("The project exceeds its authored terrain cell budget.");
            }
        }

        ValidateWorlds(ids);

        Dictionary<Guid, TextureAsset> textures = Assets.ToDictionary(a => a.Id);
        Dictionary<Guid, SpriteAtlas> atlases = [];
        int spriteCount = 0;
        foreach (AtlasAsset asset in Atlases)
        {
            if (asset is null || !ids.Add(asset.Id))
            {
                throw new InvalidDataException("Resource identities must be unique across all types.");
            }

            ValidateName(asset.Name);
            SpriteAtlas atlas = new(asset.Id, asset.TextureId, asset.Sprites);
            if (!textures.TryGetValue(atlas.TextureId, out TextureAsset? texture))
            {
                throw new InvalidDataException($"Atlas '{asset.Name}' references a missing texture.");
            }

            bool rotated = texture.ClockwiseQuarterTurns % 2 != 0;
            atlas.ValidateTexture(rotated ? texture.Height : texture.Width, rotated ? texture.Width : texture.Height);
            atlases.Add(asset.Id, atlas);
            spriteCount += atlas.Sprites.Length;
        }

        int frameCount = 0;
        foreach (AnimationAsset asset in Animations)
        {
            if (asset is null || !ids.Add(asset.Id))
            {
                throw new InvalidDataException("Resource identities must be unique across all types.");
            }

            ValidateName(asset.Name);
            SpriteAnimation animation = new(asset.Id, asset.TicksPerSecond, asset.Repeat, asset.Frames);
            foreach (AnimationFrame frame in animation.Frames)
            {
                if (!atlases.TryGetValue(frame.AtlasId, out SpriteAtlas? atlas))
                {
                    throw new InvalidDataException($"Animation '{asset.Name}' references a missing atlas.");
                }

                atlas.Find(frame.SpriteId);
            }

            frameCount += animation.Frames.Length;
        }

        if (spriteCount > 4096 || frameCount > 8192)
        {
            throw new InvalidDataException("The project exceeds its 4096-sprite or 8192-frame metadata budget.");
        }
    }

    public ImmutableArray<Guid> Dependencies(Guid id)
    {
        if (Atlases.FirstOrDefault(a => a.Id == id) is { } atlas)
        {
            return [atlas.TextureId];
        }

        if (Animations.FirstOrDefault(a => a.Id == id) is { } animation)
        {
            return [.. animation.Frames.Select(f => f.AtlasId).Distinct().Order()];
        }

        if (Worlds.FirstOrDefault(a => a.Id == id) is { } world)
        {
            return [world.TerrainId];
        }

        if (Terrains.FirstOrDefault(a => a.Id == id) is { } terrain)
        {
            return terrain.Source is null ? [] :
                [.. terrain.Source.Frames.Select(frame => frame.TextureId).OfType<Guid>().Distinct().Order()];
        }

        if (Assets.Any(a => a.Id == id) || Models.Any(a => a.Id == id))
        {
            return [];
        }

        throw new InvalidDataException($"Resource '{id}' does not exist.");
    }

    public ImmutableArray<ResourceInfo> Dependents(Guid id)
    {
        _ = Dependencies(id);
        HashSet<Guid> affected = [id];
        ResourceInfo[] resources = Resources.ToArray();
        bool changed;
        do
        {
            changed = false;
            foreach (ResourceInfo resource in resources)
            {
                if (!affected.Contains(resource.Id) && Dependencies(resource.Id).Any(affected.Contains))
                {
                    changed |= affected.Add(resource.Id);
                }
            }
        } while (changed);

        return [.. resources.Where(a => a.Id != id && affected.Contains(a.Id))];
    }

    public static void ValidateName(string name) => ProjectFiles.ValidateName(name);
    internal static void ValidateDigest(string digest) => ProjectFiles.ValidateDigest(digest);
}
