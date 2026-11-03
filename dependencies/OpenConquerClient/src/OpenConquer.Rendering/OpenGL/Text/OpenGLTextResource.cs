using OpenConquer.Rendering.OpenGL.Text.Atlas;
using OpenConquer.Rendering.Text.Layout;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL.Text;

/// <summary>
/// The OpenGL glyph atlas resources for one text layout source.
/// </summary>
internal sealed class OpenGLTextResource : IDisposable
{
    private readonly GL _gl;
    private readonly OpenGLGlyphAtlas _glyphAtlas;

    private bool _disposed;

    public OpenGLTextResource(GL gl, NativeTextLayoutSource source)
    {
        ArgumentNullException.ThrowIfNull(gl);
        ArgumentNullException.ThrowIfNull(source);

        _gl = gl;
        Source = source;
        _glyphAtlas = new OpenGLGlyphAtlas(gl, source.Atlas);
    }

    public NativeTextLayoutSource Source
    {
        get;
    }

    internal void ValidateOwner(GL gl, string parameterName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!ReferenceEquals(_gl, gl))
        {
            throw new ArgumentException("The OpenGL text resource was created by a different graphics device.", parameterName);
        }
    }

    internal void ValidateLayout(NativeTextLayout layout, string parameterName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(layout, parameterName);

        if (!ReferenceEquals(Source, layout.Source))
        {
            throw new ArgumentException("The native text layout was produced by a different layout source.", parameterName);
        }
    }

    internal void BindPage(int pageIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _glyphAtlas.BindPage(pageIndex);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _glyphAtlas.Dispose();
        }
        finally
        {
            _disposed = true;
        }
    }
}
