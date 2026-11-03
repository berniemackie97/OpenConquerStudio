namespace OpenConquer.Content.Images;

public sealed class RgbaImage
{
    internal RgbaImage(int width, int height, byte[] pixels)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width
    {
        get;
    }

    public int Height
    {
        get;
    }

    public ReadOnlyMemory<byte> Pixels
    {
        get;
    }
}
