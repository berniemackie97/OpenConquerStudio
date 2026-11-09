using System.Collections.Immutable;
using OpenConquer.Content.Map;

namespace OpenConquerStudio.Authoring;

public sealed record TerrainSource(string MapPath, string PuzzlePath, uint CatalogValue,
    uint GridHeader0, uint GridHeader1, int SceneryCount, int OverlayGroupCount)
{
    public ImmutableArray<SourcePuzzleInput> Puzzles { get; init; } = [];
    public ImmutableArray<SourceAnimationIndexInput> AnimationIndexes { get; init; } = [];
    public ImmutableArray<SourceSceneInput> Scenes { get; init; } = [];
    public ImmutableArray<SourceFrameInput> Frames { get; init; } = [];

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

        if (AnimationIndexes.IsDefault || AnimationIndexes.Length > 256 ||
            (AnimationIndexes.Length != 0 && Puzzles.IsEmpty))
        {
            throw new InvalidDataException("Captured animation indexes require an owned puzzle input inventory.");
        }
        HashSet<string> animationPaths = new(StringComparer.OrdinalIgnoreCase);
        foreach (SourceAnimationIndexInput index in AnimationIndexes)
        {
            index.Validate();
            if (!animationPaths.Add(index.Path))
            {
                throw new InvalidDataException("Animation index paths must be unique within a terrain source.");
            }
        }

        if (Scenes.IsDefault || Scenes.Length > 256 || Frames.IsDefault || Frames.Length > 8192 ||
            (Frames.Length != 0 && AnimationIndexes.IsEmpty))
        {
            throw new InvalidDataException("Scenery and frame inputs require captured animation indexes.");
        }
        HashSet<string> scenePaths = new(StringComparer.OrdinalIgnoreCase);
        foreach (SourceSceneInput scene in Scenes)
        {
            scene.Validate();
            if (!scenePaths.Add(scene.Path))
            {
                throw new InvalidDataException("Captured scenery paths must be distinct.");
            }
        }
        HashSet<string> framePaths = new(StringComparer.OrdinalIgnoreCase);
        foreach (SourceFrameInput frame in Frames)
        {
            frame.Validate();
            if (!framePaths.Add(frame.Path))
            {
                throw new InvalidDataException("Captured image frame paths must be distinct.");
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
    public ImmutableArray<SourcePuzzleCapture> PuzzleFiles
    {
        get; init;
    }

    public ImmutableArray<SourceAnimationIndexCapture> AnimationFiles
    {
        get; init;
    }

    public ImmutableArray<SourceSceneCapture> SceneFiles
    {
        get; init;
    }

    public ImmutableArray<SourceFrameCapture> FrameFiles
    {
        get; init;
    }

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

        if (!AnimationFiles.IsDefault && (PuzzleFiles.IsDefault || DecodedSource is null))
        {
            throw new InvalidDataException("Animation capture requires decoded map and puzzle inputs.");
        }

        if ((!SceneFiles.IsDefault || !FrameFiles.IsDefault) &&
            (DecodedSource is null || PuzzleFiles.IsDefault || AnimationFiles.IsDefault))
        {
            throw new InvalidDataException("Visual source capture requires map, puzzles and ANI indexes.");
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
                ImmutableArray<SourcePuzzleReference> expected = SourcePuzzleReferences.Collect(grid, scene);
                if (PuzzleFiles.Length != expected.Length)
                {
                    throw new InvalidDataException("Puzzle capture must account for every referenced map puzzle.");
                }
                long total = 0;
                for (int i = 0; i < expected.Length; i++)
                {
                    SourcePuzzleCapture capture = PuzzleFiles[i];
                    TerrainSource.ValidatePath(capture.Path);
                    if (!string.Equals(capture.Path, expected[i].Path, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("Captured puzzle paths must follow first-reference order.");
                    }
                    if (capture.Bytes is null)
                    {
                        if (expected[i].Required)
                        {
                            throw new InvalidDataException("Required positioned puzzle inputs cannot be unresolved.");
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
            if (!SceneFiles.IsDefault)
            {
                SourceSceneDependencies.Validate(SourceSceneDependencies.Collect(scene),
                    SceneFiles, ProjectDocument.MaximumTotalSceneBytes);
            }
            if (!AnimationFiles.IsDefault)
            {
                ImmutableArray<SourceAnimationRequirement> required =
                    SourceAnimationDependencies.Collect(scene, PuzzleFiles, SceneFiles);
                SourceAnimationDependencies.Validate(required, AnimationFiles,
                    ProjectDocument.MaximumTotalAnimationIndexBytes);
                if (!FrameFiles.IsDefault)
                {
                    ImmutableArray<SourceAnimationIndexInput> indexes =
                        [.. AnimationFiles.Select((capture, index) =>
                            SourceAnimationDependencies.Read(required[index], capture.Bytes))];
                    SourceFrameDependencies.Validate(SourceFrameDependencies.Collect(indexes),
                        FrameFiles, ProjectDocument.MaximumTotalFrameBytes);
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
