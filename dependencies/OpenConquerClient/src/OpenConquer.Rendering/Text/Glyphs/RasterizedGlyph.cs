namespace OpenConquer.Rendering.Text.Glyphs;

/// <summary>
/// Represents one rasterized glyph in normalized top-left grayscale coverage form.
/// </summary>
internal sealed class RasterizedGlyph
{
    private readonly byte[] _coverage;

    public RasterizedGlyph(int widthPixels, int heightPixels, int bearingLeftPixels, int topOffsetPixels, int advancePixels, ReadOnlySpan<byte> coverage)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(widthPixels);
        ArgumentOutOfRangeException.ThrowIfNegative(heightPixels);

        int expectedCoverageLength = checked(widthPixels * heightPixels);

        if (coverage.Length != expectedCoverageLength)
        {
            throw new ArgumentException($"Glyph coverage length {coverage.Length} does not match the {widthPixels}x{heightPixels} bitmap.", nameof(coverage));
        }

        WidthPixels = widthPixels;
        HeightPixels = heightPixels;
        BearingLeftPixels = bearingLeftPixels;
        TopOffsetPixels = topOffsetPixels;
        AdvancePixels = advancePixels;
        _coverage = coverage.ToArray();
    }

    /// <summary>
    /// Gets the normalized glyph bitmap width.
    /// </summary>
    public int WidthPixels
    {
        get;
    }

    /// <summary>
    /// Gets the normalized glyph bitmap height.
    /// </summary>
    public int HeightPixels
    {
        get;
    }

    /// <summary>
    /// Gets the horizontal bearing from the pen position to the bitmap's left edge.
    /// </summary>
    public int BearingLeftPixels
    {
        get;
    }

    /// <summary>
    /// Gets the vertical offset from the text origin to the bitmap's top edge.
    /// </summary>
    public int TopOffsetPixels
    {
        get;
    }

    /// <summary>
    /// Gets the horizontal pen advance after this glyph.
    /// </summary>
    public int AdvancePixels
    {
        get;
    }

    /// <summary>
    /// Gets tightly packed top-left row-major grayscale coverage.
    /// </summary>
    public ReadOnlyMemory<byte> Coverage => _coverage;
}
