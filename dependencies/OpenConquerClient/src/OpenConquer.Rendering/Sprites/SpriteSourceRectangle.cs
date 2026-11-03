namespace OpenConquer.Rendering.Sprites;

/// <summary>
/// Defines a non-empty rectangular region of a sprite texture in top-left pixel coordinates.
/// </summary>
public readonly record struct SpriteSourceRectangle
{
    public SpriteSourceRectangle(int x, int y, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int X
    {
        get;
    }

    public int Y
    {
        get;
    }

    public int Width
    {
        get;
    }

    public int Height
    {
        get;
    }

    internal long Right => (long)X + Width;

    internal long Bottom => (long)Y + Height;
}
