namespace OpenConquer.Content.Flash;

/// <summary>
/// A SWF placement matrix in stage pixels: <c>x' = A·x + C·y + Tx</c>, <c>y' = B·x + D·y + Ty</c>.
/// </summary>
public readonly record struct FlashMatrix(float A, float B, float C, float D, float Tx, float Ty)
{
    public static FlashMatrix Identity { get; } = new(1f, 0f, 0f, 1f, 0f, 0f);
}

/// <summary>
/// SWF CXFORMWITHALPHA terms as authored: 8.8 fixed-point multipliers (256 = 1) and add terms in 0..255 units.
/// </summary>
public readonly record struct FlashColorTransform(
    int RedMultiplier, int GreenMultiplier, int BlueMultiplier, int AlphaMultiplier,
    int RedAdd, int GreenAdd, int BlueAdd, int AlphaAdd)
{
    public static FlashColorTransform Identity { get; } = new(256, 256, 256, 256, 0, 0, 0, 0);
}

/// <summary>An axis-aligned rectangle in character pixels.</summary>
public readonly record struct FlashBounds(float Left, float Top, float Right, float Bottom);
