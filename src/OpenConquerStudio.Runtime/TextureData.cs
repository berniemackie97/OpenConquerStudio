namespace OpenConquerStudio.Runtime;

/// <summary>Top-left, row-major RGBA8 pixels with straight alpha.</summary>
public sealed class TextureData
{
    public const int MaximumPixelBytes = 16 * 1024 * 1024;
    public const int MaximumDimension = 4096;
    private readonly byte[] _pixels;

    public TextureData(int width, int height, ReadOnlySpan<byte> pixels)
    {
        int length = GetByteLength(width, height);
        if (pixels.Length != length)
        {
            throw new InvalidDataException("Texture pixels do not match its dimensions.");
        }

        Width = width;
        Height = height;
        _pixels = pixels.ToArray();
    }

    public int Width
    {
        get;
    }
    public int Height
    {
        get;
    }
    public ReadOnlyMemory<byte> Pixels => _pixels;

    public static int GetByteLength(int width, int height)
    {
        if (width is < 1 or > MaximumDimension || height is < 1 or > MaximumDimension ||
            (long)width * height * 4 > MaximumPixelBytes)
        {
            throw new InvalidDataException("Texture dimensions exceed the 4096-axis or 16 MiB pixel limit.");
        }

        return width * height * 4;
    }

    public TextureData Transform(bool flipHorizontal, int clockwiseQuarterTurns, CancellationToken cancellationToken = default)
    {
        if (clockwiseQuarterTurns is < 0 or > 3)
        {
            throw new InvalidDataException("Texture rotation must be 0, 1, 2, or 3 quarter turns.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!flipHorizontal && clockwiseQuarterTurns == 0)
        {
            return this;
        }

        int width = clockwiseQuarterTurns % 2 == 0 ? Width : Height;
        int height = clockwiseQuarterTurns % 2 == 0 ? Height : Width;
        byte[] pixels = new byte[_pixels.Length];
        for (int y = 0; y < Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int x = 0; x < Width; x++)
            {
                int mirroredX = flipHorizontal ? Width - 1 - x : x;
                (int targetX, int targetY) = clockwiseQuarterTurns switch
                {
                    0 => (mirroredX, y),
                    1 => (Height - 1 - y, mirroredX),
                    2 => (Width - 1 - mirroredX, Height - 1 - y),
                    _ => (y, Width - 1 - mirroredX),
                };
                _pixels.AsSpan((y * Width + x) * 4, 4).CopyTo(pixels.AsSpan((targetY * width + targetX) * 4));
            }
        }

        return new TextureData(width, height, pixels);
    }
}
