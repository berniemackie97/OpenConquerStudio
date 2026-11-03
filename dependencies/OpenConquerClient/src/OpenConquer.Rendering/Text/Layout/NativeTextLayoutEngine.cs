using System.Buffers;
using System.Text;
using OpenConquer.Rendering.Text.Atlas;
using OpenConquer.Rendering.Text.Codec;
using OpenConquer.Rendering.Text.Glyphs;

namespace OpenConquer.Rendering.Text.Layout;

/// <summary>
/// Measures and lays out encoded text using NativeText compatibility semantics.
/// </summary>
internal sealed class NativeTextLayoutEngine
{
    private const int MissingDataIconWidthPixels = 16;
    private const int MaximumUnicodeTextUnits = 4096;

    private readonly int _effectiveCodePage;
    private readonly int _lineAdvancePixels;
    private readonly NativeTextFontRecord _primaryFont;
    private readonly NativeGlyphCache _glyphCache;
    private readonly NativeTextLayoutSource _source;
    private readonly IDataIconWidthProvider? _dataIconWidthProvider;

    public NativeTextLayoutEngine(NativeTextFontRecord primaryFont, NativeTextFontRecord recordZeroFont, int effectiveCodePage, IDataIconWidthProvider? dataIconWidthProvider = null)
    {
        ArgumentNullException.ThrowIfNull(primaryFont);
        ArgumentNullException.ThrowIfNull(recordZeroFont);

        _primaryFont = primaryFont;
        _effectiveCodePage = effectiveCodePage;
        _lineAdvancePixels = checked(primaryFont.NominalPixelHeight + (primaryFont.NominalPixelHeight / 4));
        _glyphCache = new NativeGlyphCache(primaryFont, recordZeroFont, effectiveCodePage);
        _source = new NativeTextLayoutSource(primaryFont, _glyphCache.Atlas);
        _dataIconWidthProvider = dataIconWidthProvider;
    }

    public GlyphAtlas Atlas => _source.Atlas;

    public NativeTextLayoutSource Source => _source;

    public NativeTextLayout Layout(ReadOnlySpan<byte> encodedText, bool recognizeDataIcons, int dataIconWidthPixels = 0)
    {
        EncodedTextReader reader = new(encodedText, _effectiveCodePage, recognizeDataIcons);
        List<NativeTextLayoutItem> items = new(Math.Min(encodedText.Length, 256));
        int penXPixels = 0;
        int penYPixels = 0;
        int maximumWidthPixels = 0;

        while (reader.TryRead(out EncodedTextToken token))
        {
            switch (token.Kind)
            {
                case EncodedTextTokenKind.Glyph:
                    CachedGlyph glyph = _glyphCache.GetOrAdd(token.GlyphKey);

                    if (glyph.AtlasRegion is { } atlasRegion)
                    {
                        int glyphXPixels = checked(penXPixels + glyph.BearingLeftPixels);
                        int glyphYPixels = checked(penYPixels + glyph.TopOffsetPixels);
                        items.Add(NativeTextLayoutItem.CreateGlyph(token.GlyphKey, glyphXPixels, glyphYPixels, atlasRegion));
                    }

                    penXPixels = checked(penXPixels + glyph.AdvancePixels);
                    maximumWidthPixels = Math.Max(maximumWidthPixels, penXPixels);
                    break;

                case EncodedTextTokenKind.NewLine:
                    maximumWidthPixels = Math.Max(maximumWidthPixels, penXPixels);
                    penXPixels = 0;
                    penYPixels = checked(penYPixels + _lineAdvancePixels);
                    break;

                case EncodedTextTokenKind.DataIcon:
                    int widthPixels = ResolveDataIconWidth(token.DataIconIndex, dataIconWidthPixels);
                    items.Add(NativeTextLayoutItem.CreateDataIcon(token.DataIconIndex, penXPixels, penYPixels, widthPixels));
                    penXPixels = checked(penXPixels + widthPixels);
                    maximumWidthPixels = Math.Max(maximumWidthPixels, penXPixels);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported encoded text token kind {(int)token.Kind}.");
            }
        }

        maximumWidthPixels = Math.Max(maximumWidthPixels, penXPixels);
        int heightPixels = checked(penYPixels + _primaryFont.NominalPixelHeight);

        return new NativeTextLayout(_source, maximumWidthPixels, heightPixels, items);
    }

    public NativeTextLayout LayoutUnicode(ReadOnlySpan<char> text)
    {
        if (text.Length > MaximumUnicodeTextUnits)
        {
            throw new ArgumentOutOfRangeException(nameof(text), "Unicode text exceeds the supported length.");
        }

        for (int index = 0; index < text.Length;)
        {
            if (Rune.DecodeFromUtf16(text[index..], out Rune rune, out int consumed) != OperationStatus.Done || rune.Value == 0)
            {
                throw new ArgumentException("Unicode text contains an invalid scalar value or NUL.", nameof(text));
            }

            index += consumed;
        }

        List<NativeTextLayoutItem> items = new(Math.Min(text.Length, 256));
        bool hasMissingGlyphs = false;
        int penXPixels = 0;
        int penYPixels = 0;
        int maximumWidthPixels = 0;

        for (int index = 0; index < text.Length;)
        {
            _ = Rune.DecodeFromUtf16(text[index..], out Rune rune, out int consumed);
            index += consumed;

            if (rune.Value == '\n')
            {
                maximumWidthPixels = Math.Max(maximumWidthPixels, penXPixels);
                penXPixels = 0;
                penYPixels = checked(penYPixels + _lineAdvancePixels);
                continue;
            }

            CachedGlyph glyph = _glyphCache.GetOrAdd(rune);
            hasMissingGlyphs |= glyph.IsMissing;

            if (glyph.AtlasRegion is { } region)
            {
                int glyphXPixels = checked(penXPixels + glyph.BearingLeftPixels);
                int glyphYPixels = checked(penYPixels + glyph.TopOffsetPixels);
                items.Add(NativeTextLayoutItem.CreateUnicodeGlyph(rune, glyphXPixels, glyphYPixels, region));
            }

            penXPixels = checked(penXPixels + glyph.AdvancePixels);
            maximumWidthPixels = Math.Max(maximumWidthPixels, penXPixels);
        }

        int heightPixels = checked(penYPixels + _primaryFont.NominalPixelHeight);
        return new NativeTextLayout(_source, maximumWidthPixels, heightPixels, items, hasMissingGlyphs);
    }

    private int ResolveDataIconWidth(byte dataIconIndex, int explicitWidthPixels)
    {
        if (explicitWidthPixels != 0)
        {
            return explicitWidthPixels;
        }

        if (_dataIconWidthProvider is null || !_dataIconWidthProvider.TryGetWidth(dataIconIndex, out int widthPixels))
        {
            return MissingDataIconWidthPixels;
        }

        return widthPixels == 0 ? MissingDataIconWidthPixels : widthPixels;
    }
}
