using System.Text;
using OpenConquer.Rendering.Text.Fonts;
using OpenConquer.Rendering.Text.Fonts.Discovery;
using OpenConquer.Rendering.Text.Fonts.FreeType;
using OpenConquer.Rendering.Text.Glyphs;

namespace OpenConquer.Rendering.Text.Rendering;

/// <summary>
/// C3_CORE <c>Font_CheckPixel</c> (0x100030C0) as graphic.dll <c>CMyBitmap::CheckStringPixels</c> (0x10015390) calls
/// it: a character's glyph, rendered monochrome by FreeType at the nominal pixel height, passes when more than a
/// threshold of its pixels are lit. The retail FreeType revision is unknown, so this is a rasterizer seam, not a
/// pixel-exact copy.
/// </summary>
public sealed class NativeGlyphPixelCheck : IDisposable
{
    private readonly FreeTypeLibrary _library;
    private readonly IGlyphRasterizer _rasterizer;
    private bool _disposed;

    public NativeGlyphPixelCheck(string fontToken, int nominalPixelHeight)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fontToken);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nominalPixelHeight);

        FreeTypeLibrary library = new();
        try
        {
            HostFontCatalog catalog = new(new FreeTypeFontInspector(library), new SystemHostFontSource().Discover());
            _rasterizer = new FreeTypeGlyphRasterizerFactory(library, new SystemFontResolver(catalog))
                .Create(fontToken, nominalPixelHeight, antialiasEnabled: false);
            _library = library;
        }
        catch
        {
            library.Dispose();
            throw;
        }
    }

    /// <summary>
    /// True when the glyph of <paramref name="rune"/> lights more than <paramref name="threshold"/> pixels. A rune
    /// the font cannot draw lights none.
    /// </summary>
    public bool LightsMoreThan(Rune rune, int threshold)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(threshold);

        if (!_rasterizer.TryRasterizeGlyph(rune, out RasterizedGlyph? glyph) || glyph is null)
        {
            return false;
        }

        int lit = 0;
        foreach (byte coverage in glyph.Coverage.Span)
        {
            if (coverage != 0 && ++lit > threshold)
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
            _library.Dispose();
        }
    }
}
