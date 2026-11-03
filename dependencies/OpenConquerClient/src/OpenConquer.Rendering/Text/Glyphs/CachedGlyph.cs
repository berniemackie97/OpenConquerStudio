using OpenConquer.Rendering.Text.Atlas;

namespace OpenConquer.Rendering.Text.Glyphs;

/// <summary>
/// Stores layout metrics and optional atlas placement for one cached glyph.
/// </summary>
internal sealed class CachedGlyph
{
    private CachedGlyph(int? sourceFontRecordIndex, int bearingLeftPixels, int topOffsetPixels, int advancePixels, GlyphAtlasRegion? atlasRegion, bool isMissing)
    {
        SourceFontRecordIndex = sourceFontRecordIndex;
        BearingLeftPixels = bearingLeftPixels;
        TopOffsetPixels = topOffsetPixels;
        AdvancePixels = advancePixels;
        AtlasRegion = atlasRegion;
        IsMissing = isMissing;
    }

    public int? SourceFontRecordIndex
    {
        get;
    }

    public int BearingLeftPixels
    {
        get;
    }

    public int TopOffsetPixels
    {
        get;
    }

    public int AdvancePixels
    {
        get;
    }

    public GlyphAtlasRegion? AtlasRegion
    {
        get;
    }

    public bool IsMissing
    {
        get;
    }

    public bool HasBitmap => AtlasRegion.HasValue;

    public static CachedGlyph FromRasterized(int sourceFontRecordIndex, RasterizedGlyph glyph, GlyphAtlasRegion? atlasRegion)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sourceFontRecordIndex);
        ArgumentNullException.ThrowIfNull(glyph);

        bool hasBitmap = glyph.WidthPixels > 0 && glyph.HeightPixels > 0;

        if (hasBitmap != atlasRegion.HasValue)
        {
            throw new InvalidOperationException("Rasterized glyph bitmap state does not match its atlas placement.");
        }

        if (atlasRegion is { } region && (region.WidthPixels != glyph.WidthPixels || region.HeightPixels != glyph.HeightPixels))
        {
            throw new InvalidOperationException("Rasterized glyph dimensions do not match its atlas placement.");
        }

        return new CachedGlyph(sourceFontRecordIndex, glyph.BearingLeftPixels, glyph.TopOffsetPixels, glyph.AdvancePixels, atlasRegion, isMissing: false);
    }

    public static CachedGlyph Missing(int advancePixels)
    {
        return new CachedGlyph(null, 0, 0, advancePixels, null, isMissing: true);
    }
}
