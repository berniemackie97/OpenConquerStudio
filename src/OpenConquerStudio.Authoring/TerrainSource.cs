using System.Collections.Immutable;
using OpenConquer.Content.Map;

namespace OpenConquerStudio.Authoring;

public sealed record TerrainSource(string MapPath, string PuzzlePath, uint CatalogValue,
    uint GridHeader0, uint GridHeader1, int SceneryCount, int OverlayGroupCount)
{
    public ImmutableArray<SourcePuzzleInput> Puzzles { get; init; } = [];

    public string? SnapshotDigest
    {
        get; init;
    }
    public int SnapshotLength
    {
        get; init;
    }

    public void Validate()
    {
        ValidatePath(MapPath);
        ValidatePath(PuzzlePath);
        if (SceneryCount < 0 || OverlayGroupCount < 0)
        {
            throw new InvalidDataException("Terrain source record counts must be nonnegative.");
        }
        if (Puzzles.IsDefault || Puzzles.Length > 256)
        {
            throw new InvalidDataException("Map puzzle input references exceed their resource budget.");
        }
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach (SourcePuzzleInput puzzle in Puzzles)
        {
            puzzle.Validate();
            if (!paths.Add(puzzle.Path))
            {
                throw new InvalidDataException("Map puzzle input paths must be unique.");
            }
        }

        if (SnapshotDigest is null && Puzzles.Length != 0)
        {
            throw new InvalidDataException("Puzzle inputs require a captured map source.");
        }
        if (SnapshotDigest is null)
        {
            if (SnapshotLength != 0)
            {
                throw new InvalidDataException("Map snapshot length requires a digest.");
            }
        }
        else
        {
            ProjectDocument.ValidateDigest(SnapshotDigest);
            if (SnapshotLength is < 276 or > MaximumDecodedSourceBytes)
            {
                throw new InvalidDataException("Map snapshot exceeds its allowed length.");
            }
        }
    }

    public const int MaximumDecodedSourceBytes = 32 * 1024 * 1024;

    internal static void ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 260 ||
            path.Any(char.IsControl) || path.Contains('\\') ||
            path.Split('/').Any(segment => segment.Length == 0 || segment is "." or ".." || segment.Contains(':')))
        {
            throw new InvalidDataException("Terrain source path must be a safe relative content path.");
        }
    }
}

public sealed record TerrainImport(uint MapDataId, int Width, int Height,
    ImmutableArray<TerrainCell> Cells, ImmutableArray<TerrainExitMarker> Exits, TerrainSource Source)
{
    public ImmutableArray<SourcePuzzleCapture> PuzzleFiles { get; init; } = default;

    public byte[]? DecodedSource
    {
        get; init;
    }

    public void Validate()
    {
        if (MapDataId == 0 || Cells.IsDefault || Cells.Length != TerrainGrid.CellCount(Width, Height) ||
            Exits.IsDefault || Exits.Length > ProjectDocument.MaximumTerrainExitMarkers || Source is null)
        {
            throw new InvalidDataException("Terrain extraction has invalid identity, dimensions, cells, or exits.");
        }

        Source.Validate();
        if (Source.SnapshotDigest is not null)
        {
            throw new InvalidDataException("Map snapshot metadata must be assigned when importing.");
        }
        if (!PuzzleFiles.IsDefault && DecodedSource is null)
        {
            throw new InvalidDataException("Puzzle capture requires the corresponding decoded map.");
        }

        if (DecodedSource is not null)
        {
            if (DecodedSource.Length is < 276 or > TerrainSource.MaximumDecodedSourceBytes)
            {
                throw new InvalidDataException("Decoded map exceeds its size limit.");
            }

            GameMapCellGrid grid = GameMapCellGrid.Parse(DecodedSource);
            GameMapSceneSections scene = GameMapSceneSections.Parse(DecodedSource, grid.BytesConsumed, grid.HeaderField0);
            if (grid.Width != Width || grid.Height != Height ||
                grid.HeaderField0 != Source.GridHeader0 || grid.HeaderField1 != Source.GridHeader1 ||
                grid.PuzzlePath != Source.PuzzlePath ||
                scene.Scenery.Count != Source.SceneryCount || scene.PositionedOverlays.Count != Source.OverlayGroupCount ||
                scene.ExitMarkers.Count != Exits.Length)
            {
                throw new InvalidDataException("Decoded map differs from the extracted terrain metadata.");
            }
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    GameMapCell cell = grid.GetCell(x, y);
                    if (Cells[y * Width + x] != new TerrainCell(cell.SurfaceId, cell.PassabilityFlag, cell.Elevation))
                    {
                        throw new InvalidDataException("Decoded map cells differ from extracted terrain.");
                    }
                }
            }
            for (int i = 0; i < scene.ExitMarkers.Count; i++)
            {
                GameMapExitMarker marker = scene.ExitMarkers[i];
                TerrainExitMarker exit = Exits[i];
                if (exit.X != marker.Field0 || exit.Y != marker.Field1 ||
                    exit.PasswayIndex != unchecked((uint)marker.Field2))
                {
                    throw new InvalidDataException("Decoded map exits differ from extracted terrain.");
                }
            }
            if (!PuzzleFiles.IsDefault)
            {
                List<string> expected = [grid.PuzzlePath];
                foreach (GameMapPositionedOverlayRecord group in scene.PositionedOverlays)
                {
                    foreach (GameMapPuzzleOverlayEntry entry in group.Entries.OfType<GameMapPuzzleOverlayEntry>())
                    {
                        if (!expected.Contains(entry.PuzzlePath, StringComparer.OrdinalIgnoreCase))
                        {
                            expected.Add(entry.PuzzlePath);
                        }
                    }
                }
                if (PuzzleFiles.Length != expected.Count)
                {
                    throw new InvalidDataException("Puzzle capture must account for every referenced map puzzle.");
                }
                long total = 0;
                for (int i = 0; i < expected.Count; i++)
                {
                    SourcePuzzleCapture capture = PuzzleFiles[i];
                    TerrainSource.ValidatePath(capture.Path);
                    if (!string.Equals(capture.Path, expected[i], StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("Captured puzzle paths must follow first-reference order.");
                    }
                    if (capture.Bytes is null)
                    {
                        if (i != 0 || !capture.Path.EndsWith(".pul", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidDataException("Only an absent optional ground puzzle may remain unresolved.");
                        }
                    }
                    else
                    {
                        total += capture.Bytes.Length;
                        if (total > ProjectDocument.MaximumTotalPuzzleBytes)
                        {
                            throw new InvalidDataException("Puzzle inputs exceed the project capture budget.");
                        }
                        _ = SourcePuzzleInput.FromBytes(capture.Path, capture.Bytes);
                    }
                }
            }
        }

        HashSet<Guid> ids = [];
        foreach (TerrainExitMarker exit in Exits)
        {
            if (exit.Id == Guid.Empty || !ids.Add(exit.Id))
            {
                throw new InvalidDataException("Terrain extraction exit identities must be unique and nonempty.");
            }
        }
    }
}
