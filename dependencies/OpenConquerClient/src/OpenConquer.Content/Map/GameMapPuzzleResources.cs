using System.Globalization;
using OpenConquer.Content.Ani;

namespace OpenConquer.Content.Map;

/// <summary>Ground and positioned puzzle bitmaps with their authored tile animation sections.</summary>
public sealed class GameMapPuzzleResources
{
    private const int MaximumPuzzleFileBytes = 128 * 1024;
    private const int UnloadedGroundDimension = 20;

    private readonly Dictionary<string, PuzzleResource> _puzzles;

    private GameMapPuzzleResources(GameMapPuzzleBitmap? ground, Dictionary<string, PuzzleResource> puzzles)
    {
        Ground = ground;
        _puzzles = puzzles;
    }

    public GameMapPuzzleBitmap? Ground
    {
        get;
    }

    public int GroundColumns => Ground?.Columns ?? UnloadedGroundDimension;

    public int GroundRows => Ground?.Rows ?? UnloadedGroundDimension;

    public GameMapPuzzleBitmap GetRequired(string contentPath)
    {
        if (!_puzzles.TryGetValue(contentPath, out PuzzleResource? puzzle))
        {
            throw new KeyNotFoundException($"Puzzle bitmap '{contentPath}' does not belong to this map.");
        }

        return puzzle.Bitmap;
    }

    public AniIndexSection GetRequiredTileAnimation(string contentPath, ushort tileIndex)
    {
        if (!_puzzles.TryGetValue(contentPath, out PuzzleResource? puzzle))
        {
            throw new KeyNotFoundException($"Puzzle bitmap '{contentPath}' does not belong to this map.");
        }

        if (!puzzle.Animations.TryGetValue(tileIndex, out AniIndexSection? section))
        {
            throw new InvalidDataException($"Puzzle bitmap '{contentPath}' does not reference tile index {tileIndex}.");
        }

        return section;
    }

    public static GameMapPuzzleResources Load(IClientContentSource contentSource, GameMapContent map)
    {
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentNullException.ThrowIfNull(map);

        Dictionary<string, GameMapPuzzleBitmap> bitmaps = new(StringComparer.OrdinalIgnoreCase);

        GameMapPuzzleBitmap? LoadPuzzle(string path, bool allowMissing = false)
        {
            if (bitmaps.TryGetValue(path, out GameMapPuzzleBitmap? puzzle))
            {
                return puzzle;
            }

            if (!contentSource.TryOpenRead(path, ContentLookupMode.LooseOnly, out Stream? stream))
            {
                if (allowMissing)
                {
                    return null;
                }

                stream = contentSource.OpenRequiredRead(path, ContentLookupMode.LooseOnly);
            }

            using (stream)
            {
                puzzle = GameMapPuzzleBitmap.Parse(ContentReader.ReadBytes(stream, path, MaximumPuzzleFileBytes));
            }

            bitmaps.Add(path, puzzle);
            return puzzle;
        }

        string groundPath = map.CellGrid.PuzzlePath;
        GameMapPuzzleBitmap? ground = LoadPuzzle(groundPath,
            allowMissing: groundPath.EndsWith(".pul", StringComparison.OrdinalIgnoreCase));
        foreach (GameMapPositionedOverlayRecord record in map.Scene.PositionedOverlays)
        {
            foreach (GameMapPositionedOverlayEntry entry in record.Entries)
            {
                if (entry is GameMapPuzzleOverlayEntry overlay)
                {
                    LoadPuzzle(overlay.PuzzlePath);
                }
            }
        }

        AniSectionResolver animationSections = new(contentSource, ContentLookupMode.LooseOnly);
        Dictionary<string, PuzzleResource> puzzles = new(bitmaps.Count, StringComparer.OrdinalIgnoreCase);

        foreach ((string path, GameMapPuzzleBitmap bitmap) in bitmaps)
        {
            Dictionary<ushort, AniIndexSection> animations = [];
            foreach (ushort tileIndex in bitmap.Tiles)
            {
                if (tileIndex == ushort.MaxValue || animations.ContainsKey(tileIndex))
                {
                    continue;
                }

                string sectionName = $"Puzzle{tileIndex.ToString(CultureInfo.InvariantCulture)}";
                animations.Add(tileIndex, animationSections.Resolve(bitmap.TextureIndexPath, sectionName));
            }

            puzzles.Add(path, new PuzzleResource(bitmap, animations));
        }

        return new GameMapPuzzleResources(ground, puzzles);
    }

    private sealed record PuzzleResource(GameMapPuzzleBitmap Bitmap, Dictionary<ushort, AniIndexSection> Animations);
}
