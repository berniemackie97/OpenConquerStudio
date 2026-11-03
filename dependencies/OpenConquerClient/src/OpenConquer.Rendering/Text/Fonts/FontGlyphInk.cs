using System.Text;
using OpenConquer.Rendering.Text.Fonts.Discovery;
using OpenConquer.Rendering.Text.Fonts.FreeType;
using OpenConquer.Rendering.Text.Glyphs;

namespace OpenConquer.Rendering.Text.Fonts;

/// <summary>Monochrome glyph-ink test used by the retail character-name gate.</summary>
public sealed class FontGlyphInk : IDisposable
{
    private readonly FreeTypeLibrary? _library;
    private readonly IGlyphRasterizer _rasterizer;
    private bool _disposed;

    public FontGlyphInk(string fontFace, int nominalPixelHeight)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fontFace);

        FreeTypeLibrary library = new();
        try
        {
            HostFontDiscovery discovery = new SystemHostFontSource().Discover();
            HostFontCatalog catalog = new(new FreeTypeFontInspector(library), discovery);
            SystemFontResolver resolver = new(catalog);
            _rasterizer = new FreeTypeGlyphRasterizerFactory(library, resolver)
                .Create(fontFace, nominalPixelHeight, antialiasEnabled: false);
            _library = library;
        }
        catch
        {
            library.Dispose();
            throw;
        }
    }

    internal FontGlyphInk(IGlyphRasterizer rasterizer)
    {
        ArgumentNullException.ThrowIfNull(rasterizer);
        if (rasterizer.AntialiasEnabled)
        {
            throw new ArgumentException("Character-name ink validation requires monochrome glyphs.", nameof(rasterizer));
        }

        _rasterizer = rasterizer;
    }

    public bool HasMoreThanFivePixels(Rune character)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_rasterizer.TryRasterizeGlyph(character, out RasterizedGlyph? glyph) || glyph is null)
        {
            return false;
        }

        int lit = 0;
        foreach (byte pixel in glyph.Coverage.Span)
        {
            if (pixel != 0 && ++lit > 5)
            {
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _rasterizer.Dispose();
        }
        finally
        {
            _library?.Dispose();
        }
    }
}
