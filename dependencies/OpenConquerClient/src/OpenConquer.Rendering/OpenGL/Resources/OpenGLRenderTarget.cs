using System.Runtime.ExceptionServices;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL.Resources;

internal sealed class OpenGLRenderTarget : IDisposable
{
    private static readonly ColorFormatCandidate[] s_rgb565Candidates =
    [
        new(InternalFormat.Rgb565, RedBits: 5, GreenBits: 6, BlueBits: 5, AlphaBits: 0, RequiresRgb565Support: true),
        new(InternalFormat.Rgb5, RedBits: 5, GreenBits: 5, BlueBits: 5, AlphaBits: 0, RequiresRgb565Support: false),
    ];

    private static readonly ColorFormatCandidate[] s_rgba8Candidates =
    [
        new(InternalFormat.Rgba8, RedBits: 8, GreenBits: 8, BlueBits: 8, AlphaBits: 8, RequiresRgb565Support: false),
    ];

    private readonly GL _gl;
    private readonly int _width;
    private readonly int _height;
    private readonly RenderColorFormat _colorFormat;

    private uint _framebuffer;
    private uint _colorTexture;
    private uint _depthRenderbuffer;
    private bool _disposed;

    public OpenGLRenderTarget(GL gl, int width, int height, RenderColorFormat colorFormat = RenderColorFormat.Rgb565)
    {
        ArgumentNullException.ThrowIfNull(gl);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        _gl = gl;
        _width = width;
        _height = height;
        _colorFormat = Enum.IsDefined(colorFormat) ? colorFormat
            : throw new ArgumentOutOfRangeException(nameof(colorFormat), colorFormat, "Unsupported render color format.");

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
                // Preserve the original resource-creation failure.
            }

            throw;
        }
    }

    public void BeginFrame()
    {
        ObjectDisposedException.ThrowIf(_disposed, instance: this);

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        _gl.Viewport(0, 0, (uint)_width, (uint)_height);

        _gl.Disable(EnableCap.ScissorTest);
        _gl.Disable(EnableCap.Dither);
        _gl.ColorMask(red: true, green: true, blue: true, alpha: true);
        _gl.DepthMask(flag: true);

        _gl.ClearColor(red: 0f, green: 0f, blue: 0f, alpha: 1f);
        _gl.ClearDepth(1.0);
        _gl.Clear(mask: (uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
    }

    /// <summary>Makes this target current again mid-frame without clearing it.</summary>
    public void BindForDrawing()
    {
        ObjectDisposedException.ThrowIf(_disposed, instance: this);

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        _gl.Viewport(0, 0, (uint)_width, (uint)_height);
    }

    public void BindForRead()
    {
        ObjectDisposedException.ThrowIf(_disposed, instance: this);

        _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _framebuffer);
    }

    internal unsafe byte[] ReadColorTopLeftRgba()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        int rowLength = checked(_width * 4);
        byte[] pixels = GC.AllocateUninitializedArray<byte>(checked(rowLength * _height));

        _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _framebuffer);

        fixed (byte* pixelPointer = pixels)
        {
            _gl.ReadPixels(0, 0, (uint)_width, (uint)_height, PixelFormat.Rgba, PixelType.UnsignedByte, pixelPointer);
        }

        byte[] temporaryRow = GC.AllocateUninitializedArray<byte>(rowLength);

        for (int topRow = 0, bottomRow = _height - 1; topRow < bottomRow; topRow++, bottomRow--)
        {
            Span<byte> top = pixels.AsSpan(topRow * rowLength, rowLength);
            Span<byte> bottom = pixels.AsSpan(bottomRow * rowLength, rowLength);

            top.CopyTo(temporaryRow);
            bottom.CopyTo(top);
            temporaryRow.CopyTo(bottom);
        }

        return pixels;
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
        CreateDepthRenderbuffer();

        bool supportsRgb565 = SupportsRgb565();

        foreach (ColorFormatCandidate candidate in _colorFormat == RenderColorFormat.Rgba8 ? s_rgba8Candidates : s_rgb565Candidates)
        {
            if (candidate.RequiresRgb565Support && !supportsRgb565)
            {
                continue;
            }

            if (TryCreateColorFramebuffer(candidate))
            {
                return;
            }
        }

        throw new NotSupportedException(_colorFormat == RenderColorFormat.Rgba8
            ? "The OpenGL implementation cannot create a complete RGBA8 render target."
            : "The OpenGL implementation cannot create a complete 16-bit RGB render target compatible with the retail 5517 color-buffer formats.");
    }

    private void CreateDepthRenderbuffer()
    {
        _depthRenderbuffer = _gl.GenRenderbuffer();

        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthRenderbuffer);

            _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent16, (uint)_width, (uint)_height);
            GLEnum allocationError = _gl.GetError();
            if (allocationError != GLEnum.NoError)
            {
                throw new InvalidOperationException($"OpenGL could not allocate a {_width}×{_height} depth renderbuffer ({allocationError}).");
            }

            _gl.GetRenderbufferParameter(RenderbufferTarget.Renderbuffer, pname: RenderbufferParameterName.DepthSize, out int depthBits);

            if (depthBits != 16)
            {
                throw new NotSupportedException($"The OpenGL implementation reports {depthBits}-bit depth precision; the render target requires 16 bits.");
            }
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, renderbuffer: 0);
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        firstFailure?.Throw();
    }

    private bool TryCreateColorFramebuffer(ColorFormatCandidate candidate)
    {
        bool created = false;

        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _colorTexture = _gl.GenTexture();

            _gl.BindTexture(TextureTarget.Texture2D, _colorTexture);
            _gl.TexImage2D(TextureTarget.Texture2D, level: 0, candidate.InternalFormat, (uint)_width, (uint)_height, border: 0, PixelFormat.Rgb, PixelType.UnsignedByte, ReadOnlySpan<byte>.Empty);

            if (HasExpectedColorPrecision(candidate))
            {
                _framebuffer = _gl.GenFramebuffer();

                _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
                _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, textarget: TextureTarget.Texture2D, _colorTexture, level: 0);
                _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _depthRenderbuffer);

                created = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) == GLEnum.FramebufferComplete;
            }
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer: 0);
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _gl.BindTexture(TextureTarget.Texture2D, texture: 0);
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        if (!created)
        {
            try
            {
                DestroyColorResources();
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        firstFailure?.Throw();

        return created;
    }

    private bool HasExpectedColorPrecision(ColorFormatCandidate candidate)
    {
        _gl.GetTexLevelParameter(TextureTarget.Texture2D, level: 0, pname: GetTextureParameter.TextureRedSize, out int redBits);
        _gl.GetTexLevelParameter(TextureTarget.Texture2D, level: 0, pname: GetTextureParameter.TextureGreenSize, out int greenBits);
        _gl.GetTexLevelParameter(TextureTarget.Texture2D, level: 0, pname: GetTextureParameter.TextureBlueSize, out int blueBits);
        _gl.GetTexLevelParameter(TextureTarget.Texture2D, level: 0, pname: GetTextureParameter.TextureAlphaSize, out int alphaBits);

        return redBits == candidate.RedBits && greenBits == candidate.GreenBits && blueBits == candidate.BlueBits && alphaBits == candidate.AlphaBits;
    }

    private bool SupportsRgb565()
    {
        _gl.GetInteger(pname: GetPName.MajorVersion, out int majorVersion);
        _gl.GetInteger(pname: GetPName.MinorVersion, out int minorVersion);

        bool hasCoreSupport = majorVersion > 4 || majorVersion == 4 && minorVersion >= 2;

        return hasCoreSupport || _gl.IsExtensionPresent("GL_ARB_ES2_compatibility");
    }

    private void DestroyResources()
    {
        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            DestroyColorResources();
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        uint depthRenderbuffer = _depthRenderbuffer;

        _depthRenderbuffer = 0;

        if (depthRenderbuffer != 0)
        {
            try
            {
                _gl.DeleteRenderbuffer(depthRenderbuffer);
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        firstFailure?.Throw();
    }

    private void DestroyColorResources()
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

        uint colorTexture = _colorTexture;

        _colorTexture = 0;

        if (colorTexture != 0)
        {
            try
            {
                _gl.DeleteTexture(colorTexture);
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        firstFailure?.Throw();
    }

    private readonly record struct ColorFormatCandidate(InternalFormat InternalFormat, int RedBits, int GreenBits, int BlueBits, int AlphaBits, bool RequiresRgb565Support);
}
