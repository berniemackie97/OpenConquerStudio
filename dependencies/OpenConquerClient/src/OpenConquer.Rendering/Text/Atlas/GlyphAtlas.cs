using OpenConquer.Rendering.Text.Glyphs;

namespace OpenConquer.Rendering.Text.Atlas;

/// <summary>
/// Stores rasterized glyph coverage in 512x512 atlas pages.
/// </summary>
internal sealed class GlyphAtlas
{
    public const int SeparationPixels = 2;

    private readonly List<GlyphAtlasPage> _pages = [];
    private readonly List<PageAllocationState> _allocationStates = [];

    public IReadOnlyList<GlyphAtlasPage> Pages => _pages;

    public GlyphAtlasRegion? Add(RasterizedGlyph glyph)
    {
        ArgumentNullException.ThrowIfNull(glyph);

        if (glyph.WidthPixels == 0 || glyph.HeightPixels == 0)
        {
            return null;
        }

        if (glyph.WidthPixels > GlyphAtlasPage.SizePixels || glyph.HeightPixels > GlyphAtlasPage.SizePixels)
        {
            throw new InvalidOperationException($"Glyph bitmap {glyph.WidthPixels}x{glyph.HeightPixels} exceeds the {GlyphAtlasPage.SizePixels}x{GlyphAtlasPage.SizePixels} atlas page.");
        }

        for (int pageIndex = 0; pageIndex < _allocationStates.Count; pageIndex++)
        {
            if (_allocationStates[pageIndex].TryAllocate(glyph.WidthPixels, glyph.HeightPixels, out int xPixels, out int yPixels))
            {
                GlyphAtlasRegion region = new(pageIndex, xPixels, yPixels, glyph.WidthPixels, glyph.HeightPixels);
                _pages[pageIndex].WriteGlyph(glyph.Coverage.Span, glyph.WidthPixels, glyph.HeightPixels, xPixels, yPixels);
                return region;
            }
        }

        GlyphAtlasPage page = new();
        PageAllocationState state = new();

        if (!state.TryAllocate(glyph.WidthPixels, glyph.HeightPixels, out int newXPixels, out int newYPixels))
        {
            throw new InvalidOperationException($"Glyph bitmap {glyph.WidthPixels}x{glyph.HeightPixels} could not be allocated in an empty atlas page.");
        }

        int newPageIndex = _pages.Count;
        GlyphAtlasRegion newRegion = new(newPageIndex, newXPixels, newYPixels, glyph.WidthPixels, glyph.HeightPixels);

        page.WriteGlyph(glyph.Coverage.Span, glyph.WidthPixels, glyph.HeightPixels, newXPixels, newYPixels);
        _pages.Add(page);
        _allocationStates.Add(state);

        return newRegion;
    }

    private sealed class PageAllocationState
    {
        private int _nextXPixels;
        private int _rowYPixels;
        private int _rowHeightPixels;

        public bool TryAllocate(int widthPixels, int heightPixels, out int xPixels, out int yPixels)
        {
            int candidateX = _nextXPixels;
            int candidateY = _rowYPixels;
            int candidateRowHeight = _rowHeightPixels;

            if (candidateX != 0 && candidateX > GlyphAtlasPage.SizePixels - widthPixels)
            {
                candidateX = 0;
                candidateY = checked(candidateY + candidateRowHeight + SeparationPixels);
                candidateRowHeight = 0;
            }

            if (candidateY > GlyphAtlasPage.SizePixels - heightPixels)
            {
                xPixels = 0;
                yPixels = 0;
                return false;
            }

            xPixels = candidateX;
            yPixels = candidateY;

            _nextXPixels = checked(candidateX + widthPixels + SeparationPixels);
            _rowYPixels = candidateY;
            _rowHeightPixels = Math.Max(candidateRowHeight, heightPixels);

            return true;
        }
    }
}
