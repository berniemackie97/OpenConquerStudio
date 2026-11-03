namespace OpenConquer.Rendering.Text.Rendering;

/// <summary>
/// Describes one draw pass of a native text glyph relative to its laid-out position.
/// </summary>
internal readonly record struct NativeTextRenderPass(
    int OffsetXPixels,
    int OffsetYPixels,
    NativeTextVertexColors Colors
);
