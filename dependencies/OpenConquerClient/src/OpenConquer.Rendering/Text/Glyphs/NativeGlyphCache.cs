using System.Text;
using OpenConquer.Rendering.Text.Atlas;
using OpenConquer.Rendering.Text.Codec;
using OpenConquer.Rendering.Text.Layout;

namespace OpenConquer.Rendering.Text.Glyphs;

/// <summary>
/// Caches encoded glyph keys, falls back to record zero for missing glyphs and places glyphs in the atlas.
/// </summary>
/// <remarks>
/// Not thread-safe; one renderer uses it. It doesn't dispose the font rasterizers passed to it.
/// </remarks>
internal sealed class NativeGlyphCache
{
    private const int MaximumUnicodeGlyphs = 4096;
    private readonly Dictionary<ushort, CachedGlyph> _glyphs = [];
    private readonly Dictionary<Rune, CachedGlyph> _unicodeGlyphs = [];
    private readonly EncodedGlyphDecoder _decoder;
    private readonly NativeTextFontRecord _primaryFont;
    private readonly NativeTextFontRecord _recordZeroFont;

    public NativeGlyphCache(NativeTextFontRecord primaryFont, NativeTextFontRecord recordZeroFont, int effectiveCodePage)
    {
        ArgumentNullException.ThrowIfNull(primaryFont);
        ArgumentNullException.ThrowIfNull(recordZeroFont);

        if (recordZeroFont.RecordIndex != 0)
        {
            throw new ArgumentException("The missing-glyph fallback font must use font record index 0.", nameof(recordZeroFont));
        }

        _primaryFont = primaryFont;
        _recordZeroFont = recordZeroFont;
        _decoder = new EncodedGlyphDecoder(effectiveCodePage);
        Atlas = new GlyphAtlas();
    }

    public GlyphAtlas Atlas
    {
        get;
    }

    public int Count => _glyphs.Count;

    public CachedGlyph GetOrAdd(ushort glyphKey)
    {
        if (_glyphs.TryGetValue(glyphKey, out CachedGlyph? cachedGlyph))
        {
            return cachedGlyph;
        }

        CachedGlyph glyph = CreateEncodedGlyph(glyphKey);
        _glyphs.Add(glyphKey, glyph);
        return glyph;
    }

    public CachedGlyph GetOrAdd(Rune rune)
    {
        if (_unicodeGlyphs.TryGetValue(rune, out CachedGlyph? cachedGlyph))
        {
            return cachedGlyph;
        }

        if (_unicodeGlyphs.Count >= MaximumUnicodeGlyphs)
        {
            throw new InvalidOperationException("The Unicode glyph cache has exhausted its character budget.");
        }

        CachedGlyph glyph = CreateGlyph(rune);
        _unicodeGlyphs.Add(rune, glyph);
        return glyph;
    }

    private CachedGlyph CreateEncodedGlyph(ushort glyphKey)
    {
        return _decoder.TryDecode(glyphKey, out Rune rune) ? CreateGlyph(rune) : CachedGlyph.Missing(_primaryFont.LineHeightPixels);
    }

    private CachedGlyph CreateGlyph(Rune rune)
    {
        if (TryRasterize(_primaryFont, rune) is { } primaryGlyph)
        {
            return primaryGlyph;
        }

        if (_primaryFont.RecordIndex != 0 && TryRasterize(_recordZeroFont, rune) is { } fallbackGlyph)
        {
            return fallbackGlyph;
        }

        return CachedGlyph.Missing(_primaryFont.LineHeightPixels);
    }

    private CachedGlyph? TryRasterize(NativeTextFontRecord font, Rune rune)
    {
        bool found = font.Rasterizer.TryRasterizeGlyph(rune, out RasterizedGlyph? rasterizedGlyph);

        if (found != (rasterizedGlyph is not null))
        {
            throw new InvalidOperationException($"Glyph rasterizer for font record {font.RecordIndex} violated the TryRasterizeGlyph result contract.");
        }

        if (!found)
        {
            return null;
        }

        GlyphAtlasRegion? atlasRegion = Atlas.Add(rasterizedGlyph!);
        return CachedGlyph.FromRasterized(font.RecordIndex, rasterizedGlyph!, atlasRegion);
    }
}
