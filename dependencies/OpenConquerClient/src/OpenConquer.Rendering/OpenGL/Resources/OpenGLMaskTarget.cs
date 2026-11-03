using System.Runtime.ExceptionServices;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL.Resources;

/// <summary>
/// One level of mask coverage at logical resolution: an RGBA8 color attachment whose red channel is 1 where the
/// mask covers the pixel and 0 elsewhere. Masked draws sample it at their own fragment position.
/// </summary>
internal sealed class OpenGLMaskTarget : IDisposable
{
    private readonly GL _gl;
    private readonly int _width;
    private readonly int _height;

    private uint _framebuffer;
    private uint _texture;
    private bool _disposed;

    public OpenGLMaskTarget(GL gl, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(gl);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        _gl = gl;
        _width = width;
        _height = height;

        try
        {
            CreateResources();
        }
        catch
        {
            try
            {
                DestroyResources();
            }
            catch
            {
                // Preserve the original mask-target creation failure.
            }

            throw;
        }
    }

    public uint Texture => _texture;

    /// <summary>Binds the target for drawing and clears its coverage to zero.</summary>
    public void BeginCoverage()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        _gl.Viewport(0, 0, (uint)_width, (uint)_height);
        _gl.Disable(EnableCap.ScissorTest);
        _gl.ColorMask(red: true, green: true, blue: true, alpha: true);
        _gl.ClearColor(red: 0f, green: 0f, blue: 0f, alpha: 0f);
        _gl.Clear(mask: (uint)ClearBufferMask.ColorBufferBit);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            DestroyResources();
        }
        finally
        {
            _disposed = true;
        }
    }

    private void CreateResources()
    {
        _texture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _texture);
        _gl.TexImage2D(TextureTarget.Texture2D, level: 0, InternalFormat.Rgba8, (uint)_width, (uint)_height, border: 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, ReadOnlySpan<byte>.Empty);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);
        _gl.BindTexture(TextureTarget.Texture2D, texture: 0);

        _framebuffer = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _texture, level: 0);
        GLEnum status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer: 0);

        if (status != GLEnum.FramebufferComplete)
        {
            throw new NotSupportedException($"The OpenGL implementation cannot create a complete RGBA8 mask target ({status}).");
        }
    }

    private void DestroyResources()
    {
        ExceptionDispatchInfo? firstFailure = null;

        uint framebuffer = _framebuffer;
        _framebuffer = 0;

        if (framebuffer != 0)
        {
            try
            {
                _gl.DeleteFramebuffer(framebuffer);
            }
            catch (Exception exception)
            {
                firstFailure = ExceptionDispatchInfo.Capture(exception);
            }
        }

        uint texture = _texture;
        _texture = 0;

        if (texture != 0)
        {
            try
            {
                _gl.DeleteTexture(texture);
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        firstFailure?.Throw();
    }
}
