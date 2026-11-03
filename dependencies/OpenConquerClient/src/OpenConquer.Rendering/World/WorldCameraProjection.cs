using OpenConquer.Content.Map;

namespace OpenConquer.Rendering.World;

/// <summary>5517 map projection from the hero's interpolated world position.</summary>
internal static class WorldCameraProjection
{
    private const int MapScale = 256;

    public static (int X, int Y) ProjectTile(GameMapContent map, int tileX, int tileY)
    {
        ArgumentNullException.ThrowIfNull(map);

        long x = ((long)tileX - tileY) * 32 + (long)map.CellGrid.Width * 32;
        long y = ((long)tileX + tileY) * 16 + 16;
        return (checked((int)x), checked((int)y));
    }

    public static WorldPuzzleProjection FromHeroWorldPosition(
        GameMapContent map, GameMapPuzzleResources resources,
        int heroWorldX, int heroWorldY, int screenWidth, int screenHeight,
        int zoomPercent = 100, int edgeScrollX = 0, int edgeScrollY = 0)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(screenWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(screenHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zoomPercent);

        long divisor = (long)zoomPercent * MapScale / 100;
        if (divisor is < 1 or > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(zoomPercent));
        }

        long groundWidth = (long)resources.GroundColumns * 256;
        long groundHeight = (long)resources.GroundRows * 256;
        long baseX = (long)map.CellGrid.Width * 32 - groundWidth / 2;
        long baseY = 16 + (long)map.CellGrid.Height * 32 / 2 - groundHeight / 2
            - ((map.CellGrid.Height + 1L) % 2) * 16;

        long viewWidth = (long)screenWidth * MapScale / divisor;
        long viewHeight = (long)screenHeight * MapScale / divisor;
        long clampPaddingX = (screenWidth - viewWidth) / 2;
        long clampPaddingY = (screenHeight - viewHeight) / 2;

        long centerX = (long)heroWorldX - screenWidth / 2 + edgeScrollX;
        long centerY = (long)heroWorldY - screenHeight / 2 + edgeScrollY;

        // Retail applies the upper bound before the lower bound.
        centerX = Math.Max(baseX - clampPaddingX,
            Math.Min(centerX, baseX + groundWidth - screenWidth + clampPaddingX));
        centerY = Math.Max(baseY - clampPaddingY,
            Math.Min(centerY, baseY + groundHeight - screenHeight + clampPaddingY));

        return new WorldPuzzleProjection(
            screenWidth, screenHeight,
            checked((int)(baseX - centerX)), checked((int)(baseY - centerY)),
            checked((int)centerX), checked((int)centerY),
            (int)divisor, MapScale);
    }
}
