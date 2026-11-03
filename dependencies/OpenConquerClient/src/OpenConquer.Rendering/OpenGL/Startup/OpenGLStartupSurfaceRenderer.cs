using OpenConquer.Rendering.Presentation;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL.Startup;

/// <summary>
/// Renders the one shot startup logo directly to a startup window's framebuffer.
/// </summary>
public sealed class OpenGLStartupSurfaceRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly OpenGLStartupImage _image;
    private readonly int _imageWidth;
    private readonly int _imageHeight;

    private bool _disposed;

    internal OpenGLStartupSurfaceRenderer(GL gl, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        ArgumentNullException.ThrowIfNull(gl);

        _gl = gl;
        _imageWidth = width;
        _imageHeight = height;
        _image = new OpenGLStartupImage(gl, width, height, rgbaPixels);
    }

    public void Render(int framebufferWidth, int framebufferHeight, int logicalWidth, int logicalHeight)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentOutOfRangeException.ThrowIfNegative(framebufferWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(framebufferHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(logicalWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(logicalHeight);

        if (framebufferWidth == 0 || framebufferHeight == 0)
        {
            return;
        }

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer: 0);
        _gl.Disable(EnableCap.ScissorTest);
        _gl.Disable(EnableCap.FramebufferSrgb);
        _gl.Viewport(0, 0, (uint)framebufferWidth, (uint)framebufferHeight);
        _gl.ColorMask(red: true, green: true, blue: true, alpha: true);
        _gl.ClearColor(red: 0f, green: 0f, blue: 0f, alpha: 1f);
        _gl.Clear(mask: (uint)ClearBufferMask.ColorBufferBit);

        StartupSurfacePlacement placement = StartupSurfacePlacement.Compute(framebufferWidth, framebufferHeight, logicalWidth, logicalHeight, _imageWidth, _imageHeight);

        _image.DrawTopLeft(framebufferWidth, framebufferHeight, placement.Width, placement.Height);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _image.Dispose();
        }
        finally
        {
            _disposed = true;
        }
    }
}
