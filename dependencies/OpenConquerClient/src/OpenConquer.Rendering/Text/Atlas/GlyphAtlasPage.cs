namespace OpenConquer.Rendering.Text.Atlas;

/// <summary>
/// Stores one 512x512 page of normalized glyph coverage.
/// </summary>
internal sealed class GlyphAtlasPage
{
    public const int SizePixels = 512;

    private readonly byte[] _coverage = new byte[SizePixels * SizePixels];

    public ReadOnlyMemory<byte> Coverage => _coverage;

    /// <summary>
    /// Gets a monotonically increasing revision that changes after each successful write.
    /// </summary>
    public long Revision
    {
        get; private set;
    }

    internal void WriteGlyph(ReadOnlySpan<byte> coverage, int widthPixels, int heightPixels, int xPixels, int yPixels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(widthPixels, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(widthPixels, SizePixels);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(heightPixels, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(heightPixels, SizePixels);
        ArgumentOutOfRangeException.ThrowIfNegative(xPixels);
        ArgumentOutOfRangeException.ThrowIfNegative(yPixels);

        int expectedCoverageLength = checked(widthPixels * heightPixels);

        if (coverage.Length != expectedCoverageLength)
        {
            throw new ArgumentException($"Glyph coverage length {coverage.Length} does not match the {widthPixels}x{heightPixels} bitmap.", nameof(coverage));
        }

        if (xPixels > SizePixels - widthPixels)
        {
            throw new ArgumentOutOfRangeException(nameof(xPixels), xPixels, $"Glyph width {widthPixels} at X {xPixels} exceeds the {SizePixels}-pixel atlas page.");
        }

        if (yPixels > SizePixels - heightPixels)
        {
            throw new ArgumentOutOfRangeException(nameof(yPixels), yPixels, $"Glyph height {heightPixels} at Y {yPixels} exceeds the {SizePixels}-pixel atlas page.");
        }

        for (int row = 0; row < heightPixels; row++)
        {
            coverage.Slice(row * widthPixels, widthPixels).CopyTo(_coverage.AsSpan(((yPixels + row) * SizePixels) + xPixels, widthPixels));
        }

        Revision = checked(Revision + 1);
    }
}
