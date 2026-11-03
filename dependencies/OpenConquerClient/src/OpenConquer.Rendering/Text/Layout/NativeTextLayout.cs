using OpenConquer.Rendering.Text.Atlas;

namespace OpenConquer.Rendering.Text.Layout;

/// <summary>
/// Measured text size, the drawable layout items in order, and the layout source that produced them.
/// </summary>
internal sealed class NativeTextLayout
{
    private readonly NativeTextLayoutItem[] _items;

    public NativeTextLayout(NativeTextLayoutSource source, int widthPixels, int heightPixels, IEnumerable<NativeTextLayoutItem> items, bool hasMissingGlyphs = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(widthPixels);
        ArgumentNullException.ThrowIfNull(items);

        Source = source;
        WidthPixels = widthPixels;
        HeightPixels = heightPixels;
        HasMissingGlyphs = hasMissingGlyphs;
        _items = items.ToArray();
    }

    public NativeTextLayoutSource Source
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

    public bool HasMissingGlyphs
    {
        get;
    }

    public ReadOnlyMemory<NativeTextLayoutItem> Items => _items;

    public bool TryGetInkBounds(out int left, out int top, out int right, out int bottom)
    {
        long minimumX = long.MaxValue;
        long minimumY = long.MaxValue;
        long maximumX = long.MinValue;
        long maximumY = long.MinValue;

        foreach (NativeTextLayoutItem item in _items)
        {
            if (item.Kind != NativeTextLayoutItemKind.Glyph)
            {
                continue;
            }

            GlyphAtlasRegion region = item.AtlasRegion;
            minimumX = Math.Min(minimumX, item.XPixels);
            minimumY = Math.Min(minimumY, item.YPixels);
            maximumX = Math.Max(maximumX, (long)item.XPixels + region.WidthPixels);
            maximumY = Math.Max(maximumY, (long)item.YPixels + region.HeightPixels);
        }

        if (minimumX == long.MaxValue)
        {
            left = top = right = bottom = 0;
            return false;
        }

        left = checked((int)minimumX);
        top = checked((int)minimumY);
        right = checked((int)maximumX);
        bottom = checked((int)maximumY);
        return true;
    }
}
