using System.Runtime.ExceptionServices;
using OpenConquer.Rendering.Text.Atlas;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL.Text.Atlas;

/// <summary>
/// The OpenGL textures for one CPU glyph atlas.
/// </summary>
internal sealed class OpenGLGlyphAtlas : IDisposable
{
    private readonly GL _gl;
    private readonly GlyphAtlas _atlas;
    private readonly List<OpenGLGlyphAtlasPageTexture> _pageTextures = [];

    private bool _disposed;

    public OpenGLGlyphAtlas(GL gl, GlyphAtlas atlas)
    {
        ArgumentNullException.ThrowIfNull(gl);
        ArgumentNullException.ThrowIfNull(atlas);

        _gl = gl;
        _atlas = atlas;
    }

    public void BindPage(int pageIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);

        if (pageIndex >= _atlas.Pages.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageIndex),
                pageIndex,
                $"Glyph atlas contains {_atlas.Pages.Count} page(s)."
            );
        }

        EnsurePageTexturesThrough(pageIndex);
        _pageTextures[pageIndex].Bind();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ExceptionDispatchInfo? firstFailure = null;

        for (int index = _pageTextures.Count - 1; index >= 0; index--)
        {
            try
            {
                _pageTextures[index].Dispose();
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        _pageTextures.Clear();
        _disposed = true;

        firstFailure?.Throw();
    }

    private void EnsurePageTexturesThrough(int pageIndex)
    {
        while (_pageTextures.Count <= pageIndex)
        {
            GlyphAtlasPage page = _atlas.Pages[_pageTextures.Count];
            _pageTextures.Add(new OpenGLGlyphAtlasPageTexture(_gl, page));
        }
    }
}
