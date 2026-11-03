using OpenConquer.Rendering.Text.Layout;

namespace OpenConquer.Rendering.Text.Rendering;

/// <summary>
/// Builds a reusable rendering plan for one text layout, grouped by atlas page.
/// </summary>
internal sealed class NativeTextBatchPlan
{
    private readonly int _maximumGlyphPasses;

    private int[] _pageCounts = [];
    private int[] _pageOffsets = [];
    private int[] _pageWriteCursors = [];
    private int[] _usedPageIndices = [];
    private int[] _glyphItemIndices = [];

    private int _atlasPageCount;
    private bool _prepared;

    public NativeTextBatchPlan(int maximumGlyphPasses)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumGlyphPasses);
        _maximumGlyphPasses = maximumGlyphPasses;
    }

    public int MaximumGlyphPasses => _maximumGlyphPasses;

    public int GlyphCount
    {
        get; private set;
    }

    public int GlyphPassCount
    {
        get; private set;
    }

    public int UsedPageCount
    {
        get; private set;
    }

    public NativeTextRenderPassSequence PassSequence
    {
        get; private set;
    }

    public void Prepare(NativeTextLayout layout, NativeTextRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(layout);

        ResetPreparedState();

        ReadOnlySpan<NativeTextLayoutItem> items = layout.Items.Span;
        int atlasPageCount = layout.Source.Atlas.Pages.Count;
        int glyphCount = ValidateItemsAndCountGlyphs(items, atlasPageCount);

        if (glyphCount == 0)
        {
            EnsurePageCapacity(atlasPageCount);
            Array.Clear(_pageCounts, 0, atlasPageCount);
            BuildPageOffsets(atlasPageCount);

            _atlasPageCount = atlasPageCount;
            _prepared = true;
            return;
        }

        NativeTextRenderPassSequence passSequence = new(options, layout.Source.PrimaryFont);
        long glyphPassCount = (long)glyphCount * passSequence.Count;

        if (glyphPassCount > _maximumGlyphPasses)
        {
            throw new InvalidOperationException($"Native text rendering requires {glyphPassCount} glyph passes, exceeding the configured {_maximumGlyphPasses}-pass work budget.");
        }

        EnsurePageCapacity(atlasPageCount);
        EnsureGlyphItemCapacity(glyphCount);

        Array.Clear(_pageCounts, 0, atlasPageCount);

        CountGlyphsByPage(items);

        int usedPageCount = BuildPageOffsets(atlasPageCount);
        GroupGlyphItemIndices(items);

        _atlasPageCount = atlasPageCount;
        GlyphCount = glyphCount;
        GlyphPassCount = checked((int)glyphPassCount);
        UsedPageCount = usedPageCount;
        PassSequence = passSequence;
        _prepared = true;
    }

    public int GetUsedPageIndex(int usedPageIndex)
    {
        EnsurePrepared();
        ArgumentOutOfRangeException.ThrowIfNegative(usedPageIndex);

        if (usedPageIndex >= UsedPageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(usedPageIndex), usedPageIndex, $"The batch plan contains {UsedPageCount} used atlas page(s).");
        }

        return _usedPageIndices[usedPageIndex];
    }

    public ReadOnlySpan<int> GetGlyphItemIndicesForPage(int pageIndex)
    {
        EnsurePrepared();
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);

        if (pageIndex >= _atlasPageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), pageIndex, $"The batch plan was prepared against {_atlasPageCount} atlas page(s).");
        }

        return _glyphItemIndices.AsSpan(_pageOffsets[pageIndex], _pageCounts[pageIndex]);
    }

    private static int ValidateItemsAndCountGlyphs(ReadOnlySpan<NativeTextLayoutItem> items, int atlasPageCount)
    {
        int glyphCount = 0;

        foreach (NativeTextLayoutItem item in items)
        {
            switch (item.Kind)
            {
                case NativeTextLayoutItemKind.Glyph:
                    int pageIndex = item.AtlasRegion.PageIndex;

                    if (pageIndex >= atlasPageCount)
                    {
                        throw new InvalidOperationException($"Native text glyph references atlas page {pageIndex}, but its layout source currently contains {atlasPageCount} page(s).");
                    }

                    glyphCount++;
                    break;

                case NativeTextLayoutItemKind.DataIcon:
                    throw new NotSupportedException("Native text data-icon rendering requires an explicit icon rendering resource and is not supported by the glyph text renderer.");

                default:
                    throw new InvalidOperationException($"Unsupported native text layout item kind {(int)item.Kind}.");
            }
        }

        return glyphCount;
    }

    private void CountGlyphsByPage(ReadOnlySpan<NativeTextLayoutItem> items)
    {
        foreach (NativeTextLayoutItem item in items)
        {
            if (item.Kind == NativeTextLayoutItemKind.Glyph)
            {
                _pageCounts[item.AtlasRegion.PageIndex]++;
            }
        }
    }

    private int BuildPageOffsets(int atlasPageCount)
    {
        int itemOffset = 0;
        int usedPageCount = 0;

        for (int pageIndex = 0; pageIndex < atlasPageCount; pageIndex++)
        {
            _pageOffsets[pageIndex] = itemOffset;
            _pageWriteCursors[pageIndex] = itemOffset;

            int pageGlyphCount = _pageCounts[pageIndex];

            if (pageGlyphCount != 0)
            {
                _usedPageIndices[usedPageCount++] = pageIndex;
                itemOffset = checked(itemOffset + pageGlyphCount);
            }
        }

        _pageOffsets[atlasPageCount] = itemOffset;
        return usedPageCount;
    }

    private void GroupGlyphItemIndices(ReadOnlySpan<NativeTextLayoutItem> items)
    {
        for (int itemIndex = 0; itemIndex < items.Length; itemIndex++)
        {
            NativeTextLayoutItem item = items[itemIndex];

            if (item.Kind != NativeTextLayoutItemKind.Glyph)
            {
                continue;
            }

            int pageIndex = item.AtlasRegion.PageIndex;
            int destinationIndex = _pageWriteCursors[pageIndex]++;
            _glyphItemIndices[destinationIndex] = itemIndex;
        }
    }

    private void EnsurePageCapacity(int requiredPageCount)
    {
        int requiredOffsetCount = checked(requiredPageCount + 1);

        if (_pageCounts.Length >= requiredPageCount && _pageOffsets.Length >= requiredOffsetCount)
        {
            return;
        }

        int capacity = GrowCapacity(_pageCounts.Length, requiredPageCount);

        _pageCounts = new int[capacity];
        _pageWriteCursors = new int[capacity];
        _usedPageIndices = new int[capacity];
        _pageOffsets = new int[checked(capacity + 1)];
    }

    private void EnsureGlyphItemCapacity(int requiredGlyphCount)
    {
        if (_glyphItemIndices.Length >= requiredGlyphCount)
        {
            return;
        }

        _glyphItemIndices = new int[GrowCapacity(_glyphItemIndices.Length, requiredGlyphCount)];
    }

    private static int GrowCapacity(int currentCapacity, int requiredCapacity)
    {
        if (requiredCapacity <= currentCapacity)
        {
            return currentCapacity;
        }

        int capacity = Math.Max(currentCapacity, 16);

        while (capacity < requiredCapacity)
        {
            if (capacity > int.MaxValue / 2)
            {
                return requiredCapacity;
            }

            capacity *= 2;
        }

        return capacity;
    }

    private void EnsurePrepared()
    {
        if (!_prepared)
        {
            throw new InvalidOperationException("The native text batch plan has not been successfully prepared.");
        }
    }

    private void ResetPreparedState()
    {
        _prepared = false;
        _atlasPageCount = 0;
        GlyphCount = 0;
        GlyphPassCount = 0;
        UsedPageCount = 0;
        PassSequence = default;
    }
}
