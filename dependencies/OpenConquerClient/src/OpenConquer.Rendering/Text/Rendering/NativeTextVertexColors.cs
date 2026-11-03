using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Rendering.Text.Rendering;

/// <summary>
/// Defines the four native-ordered vertex colors applied to one text glyph quad.
/// </summary>
public readonly record struct NativeTextVertexColors(SpriteColor TopLeft, SpriteColor BottomLeft, SpriteColor TopRight, SpriteColor BottomRight)
{
    public static NativeTextVertexColors Solid(SpriteColor color) => new(color, color, color, color);
}
