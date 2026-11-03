using System.Runtime.ExceptionServices;
using OpenConquer.Rendering.Text.Atlas;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL.Text.Atlas;

/// <summary>
/// The OpenGL coverage texture for one CPU glyph-atlas page.
/// </summary>
internal sealed unsafe class OpenGLGlyphAtlasPageTexture : IDisposable
{
    private readonly GL _gl;
    private readonly GlyphAtlasPage _page;

    private uint _texture;
    private long _uploadedRevision = -1;
    private bool _disposed;

    public OpenGLGlyphAtlasPageTexture(GL gl, GlyphAtlasPage page)
    {
        ArgumentNullException.ThrowIfNull(gl);
        ArgumentNullException.ThrowIfNull(page);

        _gl = gl;
        _page = page;

        try
        {
            CreateTexture();
        }
        catch
        {
            try
            {
                DestroyTexture();
            }
            catch
            {
                // Preserve the original atlas-texture creation failure.
            }

            throw;
        }
    }

    internal long UploadedRevision => _uploadedRevision;

    internal void Synchronize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        long revision = _page.Revision;

        if (revision == _uploadedRevision)
        {
            return;
        }

        UploadCoverage(revision);
    }

    internal void Bind()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Synchronize();
        _gl.BindTexture(TextureTarget.Texture2D, _texture);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            DestroyTexture();
        }
        finally
        {
            _disposed = true;
        }
    }

    private void CreateTexture()
    {
        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _texture = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, _texture);
            _gl.TexParameter(
                TextureTarget.Texture2D,
                TextureParameterName.TextureMinFilter,
                (int)GLEnum.Nearest
            );
            _gl.TexParameter(
                TextureTarget.Texture2D,
                TextureParameterName.TextureMagFilter,
                (int)GLEnum.Nearest
            );
            _gl.TexParameter(
                TextureTarget.Texture2D,
                TextureParameterName.TextureWrapS,
                (int)GLEnum.ClampToEdge
            );
            _gl.TexParameter(
                TextureTarget.Texture2D,
                TextureParameterName.TextureWrapT,
                (int)GLEnum.ClampToEdge
            );
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);

            ReadOnlySpan<byte> coverage = _page.Coverage.Span;

            fixed (byte* coveragePointer = coverage)
            {
                _gl.TexImage2D(
                    TextureTarget.Texture2D,
                    level: 0,
                    InternalFormat.R8,
                    GlyphAtlasPage.SizePixels,
                    GlyphAtlasPage.SizePixels,
                    border: 0,
                    PixelFormat.Red,
                    PixelType.UnsignedByte,
                    coveragePointer
                );
            }

            _uploadedRevision = _page.Revision;
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _gl.BindTexture(TextureTarget.Texture2D, texture: 0);
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        firstFailure?.Throw();
    }

    private void UploadCoverage(long revision)
    {
        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _gl.BindTexture(TextureTarget.Texture2D, _texture);

            ReadOnlySpan<byte> coverage = _page.Coverage.Span;

            fixed (byte* coveragePointer = coverage)
            {
                _gl.TexSubImage2D(
                    TextureTarget.Texture2D,
                    level: 0,
                    xoffset: 0,
                    yoffset: 0,
                    GlyphAtlasPage.SizePixels,
                    GlyphAtlasPage.SizePixels,
                    PixelFormat.Red,
                    PixelType.UnsignedByte,
                    coveragePointer
                );
            }

            _uploadedRevision = revision;
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _gl.BindTexture(TextureTarget.Texture2D, texture: 0);
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        firstFailure?.Throw();
    }

    private void DestroyTexture()
    {
        uint texture = _texture;
        _texture = 0;
        _uploadedRevision = -1;

        if (texture != 0)
        {
            _gl.DeleteTexture(texture);
        }
    }
}
