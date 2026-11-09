using System.Collections.Immutable;
using OpenConquer.Content.Map;

namespace OpenConquerStudio.Authoring;

public sealed record SourcePuzzleReference(string Path, bool Required);

public static class SourcePuzzleReferences
{
    public const int MaximumInputs = 256;

    public static ImmutableArray<SourcePuzzleReference> Collect(GameMapCellGrid grid, GameMapSceneSections scene)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(scene);
        List<string> ordered = [grid.PuzzlePath];
        HashSet<string> known = new(StringComparer.OrdinalIgnoreCase) { grid.PuzzlePath };
        HashSet<string> required = new(StringComparer.OrdinalIgnoreCase);
        foreach (GameMapPositionedOverlayRecord group in scene.PositionedOverlays)
        {
            foreach (GameMapPuzzleOverlayEntry entry in group.Entries.OfType<GameMapPuzzleOverlayEntry>())
            {
                required.Add(entry.PuzzlePath);
                if (known.Add(entry.PuzzlePath))
                {
                    ordered.Add(entry.PuzzlePath);
                }
            }
        }
        if (ordered.Count > MaximumInputs)
        {
            throw new InvalidDataException("Map references more than 256 distinct puzzle inputs.");
        }

        return [.. ordered.Select((path, index) => new SourcePuzzleReference(path,
            index != 0 || required.Contains(path) || !path.EndsWith(".pul", StringComparison.OrdinalIgnoreCase)))];
    }
}
