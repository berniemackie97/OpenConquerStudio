namespace OpenConquer.Rendering.Sprites;

/// <summary>
/// Defines the RGBA modulation color applied to a sprite draw.
/// </summary>
public readonly record struct SpriteColor(byte Red, byte Green, byte Blue, byte Alpha)
{
    public static SpriteColor White { get; } = new(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
}
