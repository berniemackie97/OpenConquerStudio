using System.Runtime.ExceptionServices;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL.Resources;

public sealed class OpenGLTexture2D : IDisposable
{
    private readonly GL _gl;
    private uint _texture;
    private bool _disposed;

    internal OpenGLTexture2D(GL gl, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        ArgumentNullException.ThrowIfNull(gl);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        long expectedLength = checked((long)width * height * 4);

        if (expectedLength > int.MaxValue || rgbaPixels.Length != expectedLength)
        {
            throw new ArgumentException($"Expected {expectedLength} RGBA bytes for a {width}x{height} texture, but received {rgbaPixels.Length}.", nameof(rgbaPixels));
        }

        _gl = gl;
        Width = width;
        Height = height;

        try
        {
            CreateTexture(rgbaPixels);
        }
        catch
        {
            try
            {
                DestroyTexture();
            }
            catch
            {
                // Preserve the original texture-creation failure.
            }

            throw;
        }
    }

    public int Width
    {
        get;
    }

    public int Height
    {
        get;
    }

    internal void ValidateOwner(GL gl, string parameterName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!ReferenceEquals(_gl, gl))
        {
            throw new ArgumentException("The OpenGL texture was created by a different graphics device.", parameterName);
        }
    }

    internal void Bind()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

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

    private unsafe void CreateTexture(ReadOnlySpan<byte> rgbaPixels)
    {
        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _texture = _gl.GenTexture();

            _gl.BindTexture(TextureTarget.Texture2D, _texture);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);

            fixed (byte* pixelPointer = rgbaPixels)
            {
                _gl.TexImage2D(TextureTarget.Texture2D, level: 0, InternalFormat.Rgba8, (uint)Width, (uint)Height, border: 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixelPointer);
            }
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

        if (texture != 0)
        {
            _gl.DeleteTexture(texture);
        }
    }
}
