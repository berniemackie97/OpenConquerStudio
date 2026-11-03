namespace OpenConquer.Rendering.Sprites;

/// <summary>
/// Defines source-texture pixel edges for repeated sampling, including degenerate and out-of-range bounds.
/// </summary>
public readonly record struct SpriteSourceBounds
{
    public SpriteSourceBounds(int left, int top, int right, int bottom)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(left);
        ArgumentOutOfRangeException.ThrowIfNegative(top);

        if (right < left)
        {
            throw new ArgumentOutOfRangeException(nameof(right), right, "The right source edge must not precede the left edge.");
        }

        if (bottom < top)
        {
            throw new ArgumentOutOfRangeException(nameof(bottom), bottom, "The bottom source edge must not precede the top edge.");
        }

        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public int Left
    {
        get;
    }

    public int Top
    {
        get;
    }

    public int Right
    {
        get;
    }

    public int Bottom
    {
        get;
    }
}
