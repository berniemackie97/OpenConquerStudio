using System.Collections.Immutable;
using OpenConquer.Content.Map;

namespace OpenConquerStudio.Authoring;

public sealed record TerrainSceneryPlacement(Guid Id, int SourceIndex, int TypeCode,
    string ResourcePath, string? PartName, int SourceX, int SourceY, int X, int Y)
{
    public void Validate(int recordCount)
    {
        if (Id == Guid.Empty || SourceIndex < 0 || SourceIndex >= recordCount ||
            TypeCode is not (1 or 4))
        {
            throw new InvalidDataException("Scenery placement has an invalid identity or source ordinal.");
        }
        TerrainSource.ValidatePath(ResourcePath);
        if (TypeCode == 1 && PartName is not null ||
            TypeCode == 4 && (string.IsNullOrWhiteSpace(PartName) || PartName.Length > 260 ||
                PartName.Any(char.IsControl)))
        {
            throw new InvalidDataException("Scenery placement has an invalid animation section.");
        }
    }
}

public static class SourceSceneryPlacements
{
    public const int MaximumPerTerrain = 4096;

    public static ImmutableArray<TerrainSceneryPlacement> Extract(byte[] decodedMap)
    {
        ArgumentNullException.ThrowIfNull(decodedMap);
        GameMapCellGrid grid = GameMapCellGrid.Parse(decodedMap);
        GameMapSceneSections scene = GameMapSceneSections.Parse(decodedMap, grid.BytesConsumed, grid.HeaderField0);
        ImmutableArray<TerrainSceneryPlacement>.Builder result =
            ImmutableArray.CreateBuilder<TerrainSceneryPlacement>();
        for (int i = 0; i < scene.Scenery.Count; i++)
        {
            GameMapSceneryRecord record = scene.Scenery[i];
            if (record.TypeCode is not (1 or 4) ||
                record.TypeCode == 4 && GameMapSceneryAniBindings.IsEmptyAnimationRecord(record))
            {
                continue;
            }
            if (record.ResourcePath is not string path || record.TileX is not int x || record.TileY is not int y)
            {
                throw new InvalidDataException($"Scenery record {i} is missing required position or resource data.");
            }

            TerrainSceneryPlacement placement = new(Guid.NewGuid(), i, record.TypeCode,
                path, record.PartName, x, y, x, y);
            placement.Validate(scene.Scenery.Count);
            result.Add(placement);
            if (result.Count > MaximumPerTerrain)
            {
                throw new InvalidDataException("Scenery placements exceed the terrain authoring budget.");
            }
        }
        return result.MoveToImmutable();
    }

    public static ImmutableArray<TerrainSceneryPlacement> Reconcile(
        ImmutableArray<TerrainSceneryPlacement> previous, ImmutableArray<TerrainSceneryPlacement> current)
    {
        Dictionary<(int Type, string Path, string? Part), Queue<TerrainSceneryPlacement>> matches = [];
        foreach (TerrainSceneryPlacement placement in previous)
        {
            var key = Key(placement);
            if (!matches.TryGetValue(key, out Queue<TerrainSceneryPlacement>? queue))
            {
                queue = new Queue<TerrainSceneryPlacement>();
                matches.Add(key, queue);
            }
            queue.Enqueue(placement);
        }

        ImmutableArray<TerrainSceneryPlacement> result = [.. current.Select(placement =>
        {
            return matches.TryGetValue(Key(placement), out Queue<TerrainSceneryPlacement>? queue) &&
                queue.TryDequeue(out TerrainSceneryPlacement? old)
                ? placement with
                {
                    Id = old.Id,
                    X = old.X != old.SourceX ? old.X : placement.X,
                    Y = old.Y != old.SourceY ? old.Y : placement.Y
                }
                : placement;
        })];
        return previous.SequenceEqual(result) ? previous : result;
    }

    public static void ValidateEdits(ImmutableArray<TerrainSceneryPlacement> previous,
        ImmutableArray<TerrainSceneryPlacement> edited)
    {
        if (edited.IsDefault || previous.Length != edited.Length)
        {
            throw new InvalidDataException("Scenery placement edits cannot add or remove source records.");
        }
        for (int i = 0; i < previous.Length; i++)
        {
            TerrainSceneryPlacement original = previous[i];
            TerrainSceneryPlacement changed = edited[i];
            if (original.Id != changed.Id || original.SourceIndex != changed.SourceIndex ||
                original.TypeCode != changed.TypeCode || original.ResourcePath != changed.ResourcePath ||
                original.PartName != changed.PartName || original.SourceX != changed.SourceX ||
                original.SourceY != changed.SourceY)
            {
                throw new InvalidDataException("Scenery placement edits can change coordinates, not source identities or ordering.");
            }
        }
    }

    public static void ValidateAgainstSource(TerrainAsset terrain, byte[] decodedMap)
    {
        if (terrain.SceneryPlacements.IsEmpty)
        {
            return;
        }
        ImmutableArray<TerrainSceneryPlacement> required = Extract(decodedMap);
        if (required.Length != terrain.SceneryPlacements.Length)
        {
            throw new InvalidDataException("Authored scenery placement inventory differs from the captured map.");
        }
        for (int i = 0; i < required.Length; i++)
        {
            TerrainSceneryPlacement actual = terrain.SceneryPlacements[i];
            TerrainSceneryPlacement expected = required[i];
            if (actual.SourceIndex != expected.SourceIndex || actual.TypeCode != expected.TypeCode ||
                !string.Equals(actual.ResourcePath, expected.ResourcePath, StringComparison.OrdinalIgnoreCase) ||
                actual.PartName != expected.PartName || actual.SourceX != expected.SourceX ||
                actual.SourceY != expected.SourceY)
            {
                throw new InvalidDataException($"Scenery placement {i} differs from its captured source record.");
            }
        }
    }

    private static (int, string, string?) Key(TerrainSceneryPlacement placement) =>
        (placement.TypeCode, placement.ResourcePath.ToUpperInvariant(), placement.PartName);
}
