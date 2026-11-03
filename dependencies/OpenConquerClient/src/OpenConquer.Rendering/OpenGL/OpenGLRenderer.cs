using System.Numerics;
using System.Runtime.ExceptionServices;
using OpenConquer.Rendering.OpenGL.Meshes;
using OpenConquer.Rendering.OpenGL.Primitives;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.OpenGL.Sprites;
using OpenConquer.Rendering.OpenGL.Text;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;
using OpenConquer.Rendering.Text.Layout;
using OpenConquer.Rendering.Text.Rendering;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL;

public sealed class OpenGLRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly LogicalRenderSize _logicalRenderSize;
    private readonly OpenGLRenderTarget _renderTarget;
    private readonly OpenGLSpriteRenderer _spriteRenderer;
    private readonly OpenGLMeshRenderer _meshRenderer;
    private readonly OpenGLTransformedSpriteRenderer _transformedSpriteRenderer;
    private readonly OpenGLMaskStack _maskStack;
    private readonly OpenGLSolidRectangleRenderer _solidRectangleRenderer;
    private readonly OpenGLLineRectangleRenderer _lineRectangleRenderer;
    private readonly OpenGLTextRenderer _textRenderer;
    private readonly PresentationPolicy _presentationPolicy;

    private int _framebufferWidth;
    private int _framebufferHeight;
    private PresentationViewport _viewport;
    private bool _hostFramebufferValidated;
    private bool _frameActive;
    private bool _clipActive;
    private bool _disposed;

    internal OpenGLRenderer(GL gl, LogicalRenderSize logicalRenderSize, int framebufferWidth, int framebufferHeight, PresentationPolicy presentationPolicy,
        RenderColorFormat colorFormat = RenderColorFormat.Rgb565)
    {
        ArgumentNullException.ThrowIfNull(gl);
        ArgumentOutOfRangeException.ThrowIfNegative(framebufferWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(framebufferHeight);

        _gl = gl;
        _logicalRenderSize = logicalRenderSize;
        _presentationPolicy = presentationPolicy;

        OpenGLRenderTarget renderTarget = new(gl, logicalRenderSize.Width, logicalRenderSize.Height, colorFormat);
        OpenGLSpriteRenderer? spriteRenderer = null;
        OpenGLMeshRenderer? meshRenderer = null;
        OpenGLTransformedSpriteRenderer? transformedSpriteRenderer = null;
        OpenGLSolidRectangleRenderer? solidRectangleRenderer = null;
        OpenGLLineRectangleRenderer? lineRectangleRenderer = null;
        OpenGLTextRenderer? textRenderer = null;

        try
        {
            spriteRenderer = new OpenGLSpriteRenderer(gl);
            meshRenderer = new OpenGLMeshRenderer(gl);
            transformedSpriteRenderer = new OpenGLTransformedSpriteRenderer(gl);
            solidRectangleRenderer = new OpenGLSolidRectangleRenderer(gl);
            lineRectangleRenderer = new OpenGLLineRectangleRenderer(gl);
            textRenderer = new OpenGLTextRenderer(gl);

            _renderTarget = renderTarget;
            _spriteRenderer = spriteRenderer;
            _meshRenderer = meshRenderer;
            _transformedSpriteRenderer = transformedSpriteRenderer;
            _maskStack = new OpenGLMaskStack(gl, logicalRenderSize.Width, logicalRenderSize.Height);
            _solidRectangleRenderer = solidRectangleRenderer;
            _lineRectangleRenderer = lineRectangleRenderer;
            _textRenderer = textRenderer;
        }
        catch
        {
            try
            {
                meshRenderer?.Dispose();
            }
            catch
            {
                // Preserve the earlier initialization failure.
            }

            try
            {
                textRenderer?.Dispose();
            }
            catch
            {
                // Preserve the original initialization failure.
            }

            try
            {
                lineRectangleRenderer?.Dispose();
            }
            catch
            {
                // Preserve the original initialization failure.
            }

            try
            {
                solidRectangleRenderer?.Dispose();
            }
            catch
            {
                // Preserve the original initialization failure.
            }

            try
            {
                transformedSpriteRenderer?.Dispose();
            }
            catch
            {
                // Preserve the original initialization failure.
            }

            try
            {
                spriteRenderer?.Dispose();
            }
            catch
            {
                // Preserve the original initialization failure.
            }

            try
            {
                renderTarget.Dispose();
            }
            catch
            {
                // Preserve the original initialization failure.
            }

            throw;
        }

        _framebufferWidth = framebufferWidth;
        _framebufferHeight = framebufferHeight;
        _viewport = PresentationViewport.Compute(logicalRenderSize, framebufferWidth, framebufferHeight, presentationPolicy);
    }

    public PresentationViewport Viewport => _viewport;

    public void ResizeHostFramebuffer(int width, int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        _framebufferWidth = width;
        _framebufferHeight = height;
        _viewport = PresentationViewport.Compute(_logicalRenderSize, width, height, _presentationPolicy);
    }

    public void BeginFrame()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_frameActive)
        {
            throw new InvalidOperationException("A rendering frame is already active.");
        }

        _renderTarget.BeginFrame();
        _frameActive = true;
    }

    public void DrawIndexedMeshShadow(OpenGLIndexedMesh mesh, Matrix4x4 clipMatrix,
        MeshCullMode cullMode, float alpha = 0.3f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(mesh);
        EnsureFrameActiveForDrawing();
        if (_maskStack.Depth != 0)
        {
            throw new InvalidOperationException("Indexed meshes cannot be drawn inside a UI mask.");
        }

        _renderTarget.BindForDrawing();
        _meshRenderer.DrawShadow(mesh, clipMatrix, cullMode, alpha);
    }

    public void DrawIndexedMesh(OpenGLIndexedMesh mesh, OpenGLTexture2D texture,
        Matrix4x4 clipMatrix, MeshCullMode cullMode)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(texture);
        EnsureFrameActiveForDrawing();
        if (_maskStack.Depth != 0)
        {
            throw new InvalidOperationException("Indexed meshes cannot be drawn inside a UI mask.");
        }

        _renderTarget.BindForDrawing();
        _meshRenderer.Draw(mesh, texture, clipMatrix, cullMode);
    }

    public void DrawSprite(OpenGLTexture2D texture, int x, int y)
    {
        DrawSprite(texture, x, y, SpriteColor.White);
    }

    public void DrawSprite(OpenGLTexture2D texture, int x, int y, SpriteColor color)
    {
        DrawSprite(texture, x, y, color, SpriteBlendMode.Alpha);
    }

    public void DrawSprite(OpenGLTexture2D texture, int x, int y, SpriteColor color, SpriteBlendMode blendMode)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(texture);
        EnsureFrameActiveForDrawing();

        _spriteRenderer.Draw(texture, _logicalRenderSize.Width, _logicalRenderSize.Height, x, y, color, blendMode);
    }

    public void DrawSprite(OpenGLTexture2D texture, int x, int y, int width, int height)
    {
        DrawSprite(texture, x, y, width, height, SpriteColor.White);
    }

    public void DrawSprite(OpenGLTexture2D texture, int x, int y, int width, int height, SpriteColor color)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(texture);
        EnsureFrameActiveForDrawing();

        SpriteSourceRectangle sourceRectangle = new(x: 0, y: 0, texture.Width, texture.Height);

        _spriteRenderer.Draw(texture, _logicalRenderSize.Width, _logicalRenderSize.Height, sourceRectangle, x, y, width, height, color);
    }

    public void DrawSprite(OpenGLTexture2D texture, SpriteSourceRectangle sourceRectangle, int x, int y, int width, int height)
    {
        DrawSprite(texture, sourceRectangle, x, y, width, height, SpriteColor.White);
    }

    public void DrawSprite(OpenGLTexture2D texture, SpriteSourceRectangle sourceRectangle, int x, int y, int width, int height, SpriteColor color)
    {
        DrawSprite(texture, sourceRectangle, x, y, width, height, color, rotationDegrees: 0);
    }

    public void DrawSprite(OpenGLTexture2D texture, SpriteSourceRectangle sourceRectangle, int x, int y, int width, int height, SpriteColor color, int rotationDegrees)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(texture);
        EnsureFrameActiveForDrawing();

        _spriteRenderer.Draw(texture, _logicalRenderSize.Width, _logicalRenderSize.Height, sourceRectangle, x, y, width, height, color, rotationDegrees);
    }

    public void DrawRepeatedSprite(OpenGLTexture2D texture, SpriteSourceBounds sourceBounds, int x, int y, int width, int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(texture);
        EnsureFrameActiveForDrawing();

        _spriteRenderer.DrawRepeated(texture, _logicalRenderSize.Width, _logicalRenderSize.Height, sourceBounds, x, y, width, height);
    }

    /// <summary>
    /// Clips subsequent logical-frame draws to an exact top-left pixel rectangle. The renderer owns the scissor state:
    /// draw pipelines leave it alone, so a clip applies to sprites, rectangles and text alike.
    /// </summary>
    public ClipScope Clip(int x, int y, int width, int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureFrameActiveForDrawing();
        if (_clipActive)
        {
            throw new InvalidOperationException("Nested renderer clips are not supported.");
        }

        if (x < 0 || y < 0 || width <= 0 || height <= 0 ||
            (long)x + width > _logicalRenderSize.Width || (long)y + height > _logicalRenderSize.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Clip must be inside the logical render target.");
        }

        _gl.Scissor(x, _logicalRenderSize.Height - y - height, (uint)width, (uint)height);
        _gl.Enable(EnableCap.ScissorTest);
        _clipActive = true;
        return new ClipScope(this);
    }

    private void EndClip()
    {
        if (!_clipActive)
        {
            return;
        }

        _gl.Disable(EnableCap.ScissorTest);
        _clipActive = false;
    }

    public readonly struct ClipScope : IDisposable
    {
        private readonly OpenGLRenderer? _owner;
        internal ClipScope(OpenGLRenderer owner) => _owner = owner;
        public void Dispose() => _owner?.EndClip();
    }

    /// <summary>
    /// Draws a region of a premultiplied-alpha texture through <paramref name="transform"/>, which maps source-local
    /// pixels (0,0 at the region's top-left) to top-left logical pixels, sampling bilinearly and applying
    /// <paramref name="colorTransform"/> to straight color. Inside <see cref="BeginMask"/> it adds the region's
    /// shape (alpha of at least one half) to the mask instead; after <see cref="BeginMaskedContent"/> it is limited
    /// by every open mask. The other draw methods ignore masks.
    /// </summary>
    public void DrawTransformedSprite(OpenGLTexture2D texture, SpriteSourceRectangle sourceRectangle, Matrix3x2 transform,
        SpriteColorTransform colorTransform)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(texture);
        EnsureFrameActiveForDrawing();

        _transformedSpriteRenderer.Draw(texture, _logicalRenderSize.Width, _logicalRenderSize.Height, sourceRectangle, transform,
            colorTransform, _maskStack.LimitingTexture, _maskStack.IsWritingCoverage);
    }

    /// <summary>Starts a nested mask: until <see cref="BeginMaskedContent"/>, transformed sprites define its shape.</summary>
    public void BeginMask()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureFrameActiveForDrawing();

        _maskStack.Begin();
    }

    /// <summary>Ends the current mask's shapes; following transformed sprites are clipped by it.</summary>
    public void BeginMaskedContent()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureFrameActiveForDrawing();

        _maskStack.BeginContent(_renderTarget);
    }

    /// <summary>Closes the innermost mask.</summary>
    public void EndMask()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureFrameActiveForDrawing();

        _maskStack.End(_renderTarget);
    }

    public void DrawSolidRectangle(int x, int y, int width, int height, SpriteColor color)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureFrameActiveForDrawing();

        _solidRectangleRenderer.Draw(_logicalRenderSize.Width, _logicalRenderSize.Height, x, y, width, height, color);
    }

    public void DrawLineRectangle(int left, int top, int right, int bottom, SpriteColor color)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureFrameActiveForDrawing();

        _lineRectangleRenderer.Draw(_logicalRenderSize.Width, _logicalRenderSize.Height, left, top, right, bottom, color);
    }

    internal void DrawText(OpenGLTextResource resource, NativeTextLayout layout, NativeTextRenderOptions options, int x, int y)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(layout);
        EnsureFrameActiveForDrawing();

        _textRenderer.Draw(resource, layout, options, _logicalRenderSize.Width, _logicalRenderSize.Height, x, y);
    }

    internal byte[] ReadFrameTopLeftRgba()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_frameActive)
        {
            throw new InvalidOperationException("A rendering frame must be active before reading its color buffer.");
        }

        return _renderTarget.ReadColorTopLeftRgba();
    }

    public void EndFrame()
    {
        EndFrame(0);
    }

    /// <summary>Presents to a caller-owned, single-sample framebuffer in the current context.</summary>
    public void EndFrame(uint destinationFramebuffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_frameActive)
        {
            throw new InvalidOperationException("No rendering frame is active.");
        }

        bool masksOpen = _maskStack.Depth != 0;

        try
        {
            _maskStack.Reset(_renderTarget);
            EndClip();
            BlitToHostFramebuffer(destinationFramebuffer);
        }
        finally
        {
            _clipActive = false;
            _frameActive = false;
        }

        if (masksOpen)
        {
            throw new InvalidOperationException("The rendering frame ended with masks still open.");
        }
    }

    public void RenderFrame()
    {
        BeginFrame();
        EndFrame();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _meshRenderer.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _textRenderer.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _lineRectangleRenderer.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _solidRectangleRenderer.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _maskStack.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _transformedSpriteRenderer.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _spriteRenderer.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _renderTarget.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            _frameActive = false;
            _disposed = true;
        }

        firstFailure?.Throw();
    }

    private void EnsureFrameActiveForDrawing()
    {
        if (!_frameActive)
        {
            throw new InvalidOperationException("A rendering frame must be active before drawing.");
        }
    }

    private void BlitToHostFramebuffer(uint destinationFramebuffer)
    {
        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            if (!_viewport.IsEmpty)
            {
                _gl.Disable(EnableCap.ScissorTest);
                _gl.Disable(EnableCap.FramebufferSrgb);
                _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, destinationFramebuffer);

                ValidateHostFramebuffer(destinationFramebuffer);
                ClearLetterboxBars();
                _renderTarget.BindForRead();

                _gl.BlitFramebuffer(srcX0: 0, srcY0: 0, srcX1: _logicalRenderSize.Width, srcY1: _logicalRenderSize.Height,
                    dstX0: _viewport.OffsetX, dstY0: _viewport.OffsetY, dstX1: _viewport.OffsetX + _viewport.Width,
                    dstY1: _viewport.OffsetY + _viewport.Height, (uint)ClearBufferMask.ColorBufferBit, ToBlitFilter(_viewport.Filter));
            }
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, destinationFramebuffer);
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _gl.Viewport(0, 0, (uint)_framebufferWidth, (uint)_framebufferHeight);
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        firstFailure?.Throw();
    }

    private void ClearLetterboxBars()
    {
        if (_viewport.CoversHostFramebuffer())
        {
            return;
        }

        _gl.Viewport(0, 0, (uint)_framebufferWidth, (uint)_framebufferHeight);
        _gl.ColorMask(red: true, green: true, blue: true, alpha: true);
        _gl.ClearColor(red: 0f, green: 0f, blue: 0f, alpha: 1f);
        _gl.Clear(mask: (uint)ClearBufferMask.ColorBufferBit);
    }

    private static BlitFramebufferFilter ToBlitFilter(PresentationFilter filter)
    {
        return filter switch
        {
            PresentationFilter.Nearest => BlitFramebufferFilter.Nearest,
            PresentationFilter.Linear => BlitFramebufferFilter.Linear,
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, "Unknown presentation filter."),
        };
    }

    private void ValidateHostFramebuffer(uint destinationFramebuffer)
    {
        // Embedded hosts may replace attachments while retaining the same framebuffer name.
        if (destinationFramebuffer == 0 && _hostFramebufferValidated)
        {
            return;
        }

        if (_gl.CheckFramebufferStatus(FramebufferTarget.DrawFramebuffer) != GLEnum.FramebufferComplete)
        {
            throw new InvalidOperationException("The presentation framebuffer is incomplete.");
        }

        _gl.GetInteger(pname: GetPName.SampleBuffers, out int sampleBufferCount);

        if (sampleBufferCount != 0)
        {
            throw new NotSupportedException($"The current presentation path requires a single-sampled desktop framebuffer, but OpenGL reports {sampleBufferCount} sample buffer(s).");
        }

        _hostFramebufferValidated = destinationFramebuffer == 0;
    }
}
