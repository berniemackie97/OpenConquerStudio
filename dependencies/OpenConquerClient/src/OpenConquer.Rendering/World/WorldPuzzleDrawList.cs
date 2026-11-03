using OpenConquer.Content.Ani;
using OpenConquer.Content.Map;

namespace OpenConquer.Rendering.World;

internal readonly record struct WorldPuzzleTile(AniIndexSection Animation, int FrameIndex, int X, int Y, int Size);

internal readonly record struct WorldPuzzleProjection(
    int ScreenWidth, int ScreenHeight,
    int GroundOffsetX, int GroundOffsetY,
    int CenterX, int CenterY,
    int ScaleNumerator, int ScaleDenominator);

/// <summary>Retail puzzle-tile draw positions, before world entities and other map components.</summary>
internal static class WorldPuzzleDrawList
{
    private const int TileSize = 256;

    public static IReadOnlyList<WorldPuzzleTile> Ground(GameMapContent map, GameMapPuzzleResources resources,
        WorldPuzzleProjection projection, uint frame)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(resources);
        Validate(projection);

        if (resources.Ground is not { } bitmap)
        {
            return [];
        }

        List<WorldPuzzleTile> tiles = [];
        Append(tiles, resources, bitmap, map.CellGrid.PuzzlePath, projection.GroundOffsetX, projection.GroundOffsetY,
            projection, frame);
        return tiles;
    }

    public static IReadOnlyList<WorldPuzzleTile> PositionedPuzzle(
        GameMapContent map, GameMapPuzzleResources resources, WorldPuzzleProjection projection,
        GameMapPositionedOverlayRecord record, GameMapPuzzleOverlayEntry puzzle, uint frame)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(puzzle);
        Validate(projection);

        long halfMapWidth = (long)map.CellGrid.Width * 64 / 2;
        long halfMapHeight = (long)map.CellGrid.Height * 32 / 2;
        long rootX = (long)record.HeaderInt0 *
            (projection.CenterX + (long)projection.ScreenWidth / 2 - halfMapWidth) / 100
            + halfMapWidth - projection.ScreenWidth / 2;
        long rootY = (long)record.HeaderInt1 *
            (projection.CenterY + (long)projection.ScreenHeight / 2 - halfMapHeight) / 100
            + halfMapHeight - projection.ScreenHeight / 2;

        GameMapPuzzleBitmap bitmap = resources.GetRequired(puzzle.PuzzlePath);
        long bitmapWidth = (long)bitmap.Columns * TileSize;
        long bitmapHeight = (long)bitmap.Rows * TileSize;
        long offsetX = halfMapWidth - bitmapWidth / 2 - rootX;
        long offsetY = halfMapHeight - bitmapHeight / 2 - rootY;

        if (bitmapWidth >= projection.ScreenWidth && bitmapHeight >= projection.ScreenHeight)
        {
            offsetX = Math.Clamp(offsetX, projection.ScreenWidth - bitmapWidth, 0);
            offsetY = Math.Clamp(offsetY, projection.ScreenHeight - bitmapHeight, 0);
        }

        List<WorldPuzzleTile> tiles = [];
        Append(tiles, resources, bitmap, puzzle.PuzzlePath,
            checked((int)offsetX), checked((int)offsetY), projection, frame);
        return tiles;
    }

    private static void Append(List<WorldPuzzleTile> result, GameMapPuzzleResources resources,
        GameMapPuzzleBitmap bitmap, string path, int offsetX, int offsetY, WorldPuzzleProjection projection, uint frame)
    {
        // Scrolling puzzle offsets are time-dependent; a static frame must not render them as stationary.
        if (bitmap.ScrollVelocityX != 0 || bitmap.ScrollVelocityY != 0)
        {
            throw new NotSupportedException("Scrolling PUZZLE2 playback needs the retail scrolling clock.");
        }

        int size = checked((int)((long)projection.ScaleNumerator * TileSize / projection.ScaleDenominator));
        if (size < 1)
        {
            throw new InvalidDataException("Projection makes puzzle tiles smaller than one pixel.");
        }

        int startCol = -(offsetX / TileSize);
        int startRow = -(offsetY / TileSize);
        if (offsetX > 0)
        {
            startCol--;
        }

        if (offsetY > 0)
        {
            startRow--;
        }

        int colsAtScale = projection.ScreenWidth / size;
        int rowsAtScale = projection.ScreenHeight / size;
        int colCorrection = projection.ScreenWidth / TileSize - colsAtScale;
        int rowCorrection = projection.ScreenHeight / TileSize - rowsAtScale;
        int firstCol = 0;
        int firstRow = 0;
        int lastCol = colsAtScale + 3;
        int lastRow = rowsAtScale + 3;

        if (size > TileSize)
        {
            lastCol += colCorrection;
            lastRow += rowCorrection;
        }
        else if (size < TileSize)
        {
            lastCol += colCorrection + 1;
            lastRow += rowCorrection + 1;
            firstCol = -1;
            firstRow = -1;
        }

        ReadOnlySpan<ushort> indices = bitmap.Tiles;
        for (int row = firstRow; row < lastRow; row++)
        {
            int gridRow = row + startRow;
            if ((uint)gridRow >= (uint)bitmap.Rows)
            {
                continue;
            }

            for (int col = firstCol; col < lastCol; col++)
            {
                int gridCol = col + startCol;
                if ((uint)gridCol >= (uint)bitmap.Columns)
                {
                    continue;
                }

                ushort tileIndex = indices[gridRow * bitmap.Columns + gridCol];
                if (tileIndex == ushort.MaxValue)
                {
                    continue;
                }

                long x = (long)TileSize * (col + startCol) + offsetX;
                long y = (long)TileSize * (row + startRow) + offsetY;
                x = (long)projection.ScaleNumerator * (x - projection.ScreenWidth / 2) / projection.ScaleDenominator
                    + projection.ScreenWidth / 2;
                y = (long)projection.ScaleNumerator * (y - projection.ScreenHeight / 2) / projection.ScaleDenominator
                    + projection.ScreenHeight / 2;

                if (x + size <= 0 || y + size <= 0 || x >= projection.ScreenWidth || y >= projection.ScreenHeight)
                {
                    continue;
                }

                AniIndexSection animation = resources.GetRequiredTileAnimation(path, tileIndex);
                result.Add(new WorldPuzzleTile(animation, (int)(frame % (uint)animation.FrameCount),
                    checked((int)x), checked((int)y), size));
            }
        }
    }

    private static void Validate(WorldPuzzleProjection projection)
    {
        if (projection.ScreenWidth <= 0 || projection.ScreenHeight <= 0 ||
            projection.ScaleNumerator <= 0 || projection.ScaleDenominator <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(projection));
        }
    }
}
