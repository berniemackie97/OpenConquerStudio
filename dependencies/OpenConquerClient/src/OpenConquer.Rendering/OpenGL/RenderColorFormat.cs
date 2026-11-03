namespace OpenConquer.Rendering.OpenGL;

/// <summary>Color format of the renderer's logical render target.</summary>
public enum RenderColorFormat
{
    /// <summary>16-bit RGB (565, or 555 where 565 is unavailable), the retail Direct3D back-buffer formats.</summary>
    Rgb565,

    /// <summary>8 bits per channel. Blending stays in gamma space, as Flash and Direct3D 9 blended.</summary>
    Rgba8,
}
