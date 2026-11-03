using OpenConquer.Content.Ani;
using OpenConquer.Content.Map;

namespace OpenConquer.Rendering.World;

internal readonly record struct WorldScenerySprite(
    AniIndexSection Animation, int FrameIndex, int X, int Y,
    int ScaleNumerator, int ScaleDenominator,
    int TileX, int TileY, int FootprintWidth, int FootprintHeight,
    GameMapSceneryRecord? Source = null)
{
    internal bool IntersectsViewport(int width, int height, int screenWidth, int screenHeight) =>
        width > 0 && height > 0 && (long)X + width > 0 && (long)Y + height > 0 &&
        X < screenWidth && Y < screenHeight;
}
