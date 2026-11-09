using System.Collections.Immutable;
using OpenConquer.Content.Map;

namespace OpenConquerStudio.Authoring;

public enum SourceMapReferenceKind
{
    GroundPuzzle,
    Scenery,
    SceneryAnimation,
    PositionedPuzzle,
    PositionedAnimation
}

public sealed record SourceMapReference(SourceMapReferenceKind Kind, string ResourcePath,
    string? PartName, int RecordIndex, int EntryIndex);

public sealed record SourceMapScenery(int Index, int TypeCode, string PayloadDigest);

public sealed record SourceMapOverlayGroup(int Index, int InsertIndex, int HeaderField0,
    int HeaderField1, ImmutableArray<SourceMapReference> Entries);

public sealed record SourceMapInventory(int FormatVersion, uint MapDataId, string MapPath,
    string SnapshotDigest, int Width, int Height, string GroundPuzzle,
    int ExitCount, ImmutableArray<SourceMapScenery> Scenery,
    ImmutableArray<SourceMapOverlayGroup> Overlays, ImmutableArray<SourceMapReference> References)
{
    public ImmutableArray<SourcePuzzleInput> Puzzles { get; init; } = [];
    public ImmutableArray<SourceAnimationIndexInput> AnimationIndexes { get; init; } = [];
    public ImmutableArray<SourceSceneInput> Scenes { get; init; } = [];
    public ImmutableArray<SourceFrameInput> Frames { get; init; } = [];

    internal static SourceMapInventory Read(uint id, TerrainSource source, byte[] decoded)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(decoded);
        source.Validate();
        if (source.SnapshotDigest is not string digest || decoded.Length != source.SnapshotLength ||
            ProjectFiles.Hash(decoded) != digest)
        {
            throw new InvalidDataException("Map source does not match its recorded immutable digest.");
        }

        GameMapCellGrid grid = GameMapCellGrid.Parse(decoded);
        GameMapSceneSections scene = GameMapSceneSections.Parse(decoded, grid.BytesConsumed, grid.HeaderField0);
        if (grid.PuzzlePath != source.PuzzlePath ||
            grid.HeaderField0 != source.GridHeader0 || grid.HeaderField1 != source.GridHeader1 ||
            scene.Scenery.Count != source.SceneryCount || scene.PositionedOverlays.Count != source.OverlayGroupCount)
        {
            throw new InvalidDataException("Captured source-map records differ from project metadata.");
        }

        ImmutableArray<SourceMapReference>.Builder references = ImmutableArray.CreateBuilder<SourceMapReference>();
        references.Add(new SourceMapReference(SourceMapReferenceKind.GroundPuzzle,
            grid.PuzzlePath, null, -1, -1));

        ImmutableArray<SourceMapScenery>.Builder scenery = ImmutableArray.CreateBuilder<SourceMapScenery>(scene.Scenery.Count);
        for (int i = 0; i < scene.Scenery.Count; i++)
        {
            GameMapSceneryRecord record = scene.Scenery[i];
            scenery.Add(new SourceMapScenery(i, record.TypeCode, ProjectFiles.Hash(record.EncodedPayload.Span)));
            if (record.ResourcePath is string path)
            {
                references.Add(new SourceMapReference(record.TypeCode == 4
                    ? SourceMapReferenceKind.SceneryAnimation : SourceMapReferenceKind.Scenery,
                    path, record.PartName, i, -1));
            }
        }

        ImmutableArray<SourceMapOverlayGroup>.Builder overlays =
            ImmutableArray.CreateBuilder<SourceMapOverlayGroup>(scene.PositionedOverlays.Count);
        for (int i = 0; i < scene.PositionedOverlays.Count; i++)
        {
            GameMapPositionedOverlayRecord group = scene.PositionedOverlays[i];
            ImmutableArray<SourceMapReference>.Builder entries =
                ImmutableArray.CreateBuilder<SourceMapReference>(group.Entries.Count);
            for (int j = 0; j < group.Entries.Count; j++)
            {
                SourceMapReference reference = group.Entries[j] switch
                {
                    GameMapPuzzleOverlayEntry puzzle => new(SourceMapReferenceKind.PositionedPuzzle,
                        puzzle.PuzzlePath, null, i, j),
                    GameMapAnimatedOverlayEntry animation => new(SourceMapReferenceKind.PositionedAnimation,
                        animation.ResourcePath, animation.PartName, i, j),
                    _ => throw new InvalidDataException("Source map contains an unsupported overlay record.")
                };
                entries.Add(reference);
                references.Add(reference);
            }

            overlays.Add(new SourceMapOverlayGroup(i, group.InsertIndex, group.HeaderInt0,
                group.HeaderInt1, entries.MoveToImmutable()));
        }

        ImmutableArray<SourceMapReference> ordered = references.ToImmutable();
        if (!source.Puzzles.IsEmpty)
        {
            ImmutableArray<SourcePuzzleReference> puzzlePaths = SourcePuzzleReferences.Collect(grid, scene);
            if (puzzlePaths.Length != source.Puzzles.Length ||
                puzzlePaths.Where((item, index) =>
                    !string.Equals(item.Path, source.Puzzles[index].Path, StringComparison.OrdinalIgnoreCase)).Any())
            {
                throw new InvalidDataException("Captured puzzle inputs do not match their source-map references.");
            }
        }

        if (!source.Scenes.IsEmpty || !source.Frames.IsEmpty)
        {
            ImmutableArray<string> requiredScenes = SourceSceneDependencies.Collect(scene);
            if (requiredScenes.Length != source.Scenes.Length ||
                requiredScenes.Where((path, index) =>
                    !string.Equals(path, source.Scenes[index].Path, StringComparison.OrdinalIgnoreCase)).Any())
            {
                throw new InvalidDataException("Captured scenery inputs do not match source references.");
            }
            ImmutableArray<string> requiredFrames = SourceFrameDependencies.Collect(source.AnimationIndexes);
            if (requiredFrames.Length != source.Frames.Length ||
                requiredFrames.Where((path, index) =>
                    !string.Equals(path, source.Frames[index].Path, StringComparison.OrdinalIgnoreCase)).Any())
            {
                throw new InvalidDataException("Captured image frames do not match source ANI references.");
            }
        }

        return new SourceMapInventory(!source.Scenes.IsEmpty || !source.Frames.IsEmpty ? 4 :
            !source.AnimationIndexes.IsEmpty ? 3 : source.Puzzles.IsEmpty ? 1 : 2,
            id, source.MapPath, digest, grid.Width, grid.Height,
            grid.PuzzlePath, scene.ExitMarkers.Count, scenery.MoveToImmutable(),
            overlays.MoveToImmutable(), ordered)
        {
            Puzzles = source.Puzzles,
            AnimationIndexes = source.AnimationIndexes,
            Scenes = source.Scenes,
            Frames = source.Frames
        };
    }

    public static void Export(StudioProject project, Guid terrainId, string destination,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        project.ThrowIfDisposed();
        if (project.IsModified)
        {
            throw new InvalidOperationException("Save the project before exporting the source-map inventory.");
        }

        destination = ProjectFiles.RequireSafePath(destination);
        if (File.Exists(destination) || Directory.Exists(destination))
        {
            throw new IOException("The source-map inventory destination already exists.");
        }

        token.ThrowIfCancellationRequested();
        SourceMapInventory inventory = project.ReadSourceMapInventory(terrainId);
        byte[] bytes = ProjectFiles.Serialize(inventory);
        token.ThrowIfCancellationRequested();
        ProjectFiles.WriteAtomic(destination, bytes, overwrite: false);
    }
}
