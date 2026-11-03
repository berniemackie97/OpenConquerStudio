using System.Text;

namespace OpenConquer.Rendering.Text.Glyphs;

/// <summary>
/// Rasterizes Unicode scalar values using one configured font face.
/// </summary>
internal interface IGlyphRasterizer : IDisposable
{
    /// <summary>
    /// Gets whether this rasterizer was configured for antialiased glyph rendering.
    /// </summary>
    bool AntialiasEnabled
    {
        get;
    }

    /// <summary>
    /// Attempts to rasterize <paramref name="rune"/>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the configured font contains the requested glyph, otherwise, <see langword="false"/>.
    /// </returns>
    bool TryRasterizeGlyph(Rune rune, out RasterizedGlyph? glyph);
}
