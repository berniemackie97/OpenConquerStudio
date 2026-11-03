using OpenConquer.Rendering.Text.Glyphs;

namespace OpenConquer.Rendering.Text.Layout;

/// <summary>
/// Describes one configured native text font record used by layout, rendering policy, and glyph fallback.
/// </summary>
internal sealed class NativeTextFontRecord
{
    public NativeTextFontRecord(int recordIndex, int nominalPixelHeight, int lineHeightPixels, IGlyphRasterizer rasterizer)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(recordIndex);
        ArgumentNullException.ThrowIfNull(rasterizer);

        if (nominalPixelHeight == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nominalPixelHeight), nominalPixelHeight, "Nominal pixel height cannot be zero.");
        }

        if (lineHeightPixels == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineHeightPixels), lineHeightPixels, "Line height cannot be zero.");
        }

        RecordIndex = recordIndex;
        NominalPixelHeight = nominalPixelHeight;
        LineHeightPixels = lineHeightPixels;
        Rasterizer = rasterizer;
    }

    public int RecordIndex
    {
        get;
    }

    public int NominalPixelHeight
    {
        get;
    }

    public int LineHeightPixels
    {
        get;
    }

    public bool AntialiasEnabled => Rasterizer.AntialiasEnabled;

    public IGlyphRasterizer Rasterizer
    {
        get;
    }
}
