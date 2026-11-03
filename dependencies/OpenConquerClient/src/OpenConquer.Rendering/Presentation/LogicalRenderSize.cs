namespace OpenConquer.Rendering.Presentation;

public readonly record struct LogicalRenderSize
{
    public LogicalRenderSize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
    }

    public int Width
    {
        get;
    }

    public int Height
    {
        get;
    }
}
