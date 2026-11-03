namespace OpenConquer.Rendering.Sprites;

/// <summary>
/// Defines how a sprite is blended with the current render target.
/// </summary>
public enum SpriteBlendMode
{
    /// <summary>
    /// Blends the sprite using its source alpha.
    /// </summary>
    Alpha = 0,

    /// <summary>
    /// Adds the sprite's rendered color to the current render target.
    /// </summary>
    Additive = 1,
}
