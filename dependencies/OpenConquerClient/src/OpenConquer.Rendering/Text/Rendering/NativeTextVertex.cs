using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Rendering.Text.Rendering;

/// <summary>
/// Represents one native text vertex before backend-specific coordinate conversion.
/// </summary>
internal readonly record struct NativeTextVertex(float ScreenX, float ScreenY, float TextureU, float TextureV, SpriteColor Color);
