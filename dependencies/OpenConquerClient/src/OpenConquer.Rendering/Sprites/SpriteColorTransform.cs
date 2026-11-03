namespace OpenConquer.Rendering.Sprites;

/// <summary>
/// A per-channel color transform in the Flash CXFORM form: <c>channel' = channel * multiplier + offset</c>, applied to
/// straight (non-premultiplied) color in the 0..1 range and clamped. Offsets are in the same 0..1 units, so a SWF
/// add term of 255 is an offset of 1.
/// </summary>
public readonly record struct SpriteColorTransform(
    float RedMultiplier, float GreenMultiplier, float BlueMultiplier, float AlphaMultiplier,
    float RedOffset, float GreenOffset, float BlueOffset, float AlphaOffset)
{
    public static SpriteColorTransform Identity { get; } = new(1f, 1f, 1f, 1f, 0f, 0f, 0f, 0f);

    public bool IsIdentity => this == Identity;

    /// <summary>
    /// Converts SWF CXFORMWITHALPHA terms: 8.8 fixed-point multipliers (256 = 1) and add terms in 0..255 units.
    /// </summary>
    public static SpriteColorTransform FromSwf(int redMultiplier, int greenMultiplier, int blueMultiplier, int alphaMultiplier,
        int redAdd, int greenAdd, int blueAdd, int alphaAdd) =>
        new(redMultiplier / 256f, greenMultiplier / 256f, blueMultiplier / 256f, alphaMultiplier / 256f,
            redAdd / 255f, greenAdd / 255f, blueAdd / 255f, alphaAdd / 255f);

    /// <summary>
    /// Returns the transform that applies <paramref name="inner"/> first and then this one, as a parent's transform
    /// applies to everything its child draws.
    /// </summary>
    public SpriteColorTransform Concatenate(SpriteColorTransform inner) =>
        new(RedMultiplier * inner.RedMultiplier, GreenMultiplier * inner.GreenMultiplier,
            BlueMultiplier * inner.BlueMultiplier, AlphaMultiplier * inner.AlphaMultiplier,
            RedMultiplier * inner.RedOffset + RedOffset, GreenMultiplier * inner.GreenOffset + GreenOffset,
            BlueMultiplier * inner.BlueOffset + BlueOffset, AlphaMultiplier * inner.AlphaOffset + AlphaOffset);
}
