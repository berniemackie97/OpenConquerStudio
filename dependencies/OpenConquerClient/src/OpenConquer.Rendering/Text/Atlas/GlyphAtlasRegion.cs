namespace OpenConquer.Rendering.Text.Atlas;

/// <summary>
/// Identifies one glyph bitmap stored inside a glyph-atlas page.
/// </summary>
internal readonly record struct GlyphAtlasRegion
{
    public GlyphAtlasRegion(int pageIndex, int xPixels, int yPixels, int widthPixels, int heightPixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(xPixels);
        ArgumentOutOfRangeException.ThrowIfNegative(yPixels);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(widthPixels, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(heightPixels, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(widthPixels, GlyphAtlasPage.SizePixels);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(heightPixels, GlyphAtlasPage.SizePixels);

        if (xPixels > GlyphAtlasPage.SizePixels - widthPixels)
        {
            throw new ArgumentOutOfRangeException(nameof(xPixels), xPixels, $"Region width {widthPixels} at X {xPixels} exceeds the {GlyphAtlasPage.SizePixels}-pixel atlas page.");
        }

        if (yPixels > GlyphAtlasPage.SizePixels - heightPixels)
        {
            throw new ArgumentOutOfRangeException(nameof(yPixels), yPixels, $"Region height {heightPixels} at Y {yPixels} exceeds the {GlyphAtlasPage.SizePixels}-pixel atlas page.");
        }

        PageIndex = pageIndex;
        XPixels = xPixels;
        YPixels = yPixels;
        WidthPixels = widthPixels;
        HeightPixels = heightPixels;
    }

    public int PageIndex
    {
        get;
    }

    public int XPixels
    {
        get;
    }

    public int YPixels
    {
        get;
    }

    public int WidthPixels
    {
        get;
    }

    public int HeightPixels
    {
        get;
    }
}
