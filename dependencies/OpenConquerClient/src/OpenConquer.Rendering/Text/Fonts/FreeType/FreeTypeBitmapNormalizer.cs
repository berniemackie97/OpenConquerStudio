using OpenConquer.Rendering.Text.Native;

namespace OpenConquer.Rendering.Text.Fonts.FreeType;

/// <summary>
/// Normalizes FreeType glyph bitmaps into contiguous 8bit coverage data.
/// </summary>
internal static unsafe class FreeTypeBitmapNormalizer
{
    public static byte[] CopyCoverage(FreeTypeBitmap bitmap)
    {
        int widthPixels = checked((int)bitmap.Width);
        int heightPixels = checked((int)bitmap.Rows);

        return CopyCoverage((nint)bitmap.Buffer, widthPixels, heightPixels, bitmap.Pitch, bitmap.PixelMode, bitmap.GrayLevels);
    }

    internal static byte[] CopyCoverage(nint buffer, int widthPixels, int heightPixels, int pitch, FreeTypePixelMode pixelMode, ushort grayLevels)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(widthPixels);
        ArgumentOutOfRangeException.ThrowIfNegative(heightPixels);

        int coverageLength = checked(widthPixels * heightPixels);

        if (coverageLength == 0)
        {
            return [];
        }

        if (buffer == 0)
        {
            throw new InvalidOperationException("FreeType returned a non-empty glyph bitmap with no pixel buffer.");
        }

        return pixelMode switch
        {
            FreeTypePixelMode.Gray => CopyGrayscaleCoverage(buffer, widthPixels, heightPixels, pitch, grayLevels),
            FreeTypePixelMode.Mono => CopyMonochromeCoverage(buffer, widthPixels, heightPixels, pitch),
            _ => throw new InvalidOperationException($"FreeType returned unsupported glyph pixel mode {(int)pixelMode}."),
        };
    }

    private static byte[] CopyGrayscaleCoverage(nint buffer, int widthPixels, int heightPixels, int pitch, ushort grayLevels)
    {
        ValidatePitch(pitch, widthPixels);

        if (grayLevels is < 2 or > 256)
        {
            throw new InvalidOperationException($"FreeType returned invalid grayscale level count {grayLevels}.");
        }

        byte[] coverage = new byte[checked(widthPixels * heightPixels)];

        for (int row = 0; row < heightPixels; row++)
        {
            byte* source = GetLogicalRow(buffer, pitch, row, heightPixels);
            Span<byte> destination = coverage.AsSpan(checked(row * widthPixels), widthPixels);

            if (grayLevels == 256)
            {
                new ReadOnlySpan<byte>(source, widthPixels).CopyTo(destination);
                continue;
            }

            int maximumSourceValue = grayLevels - 1;

            for (int column = 0; column < widthPixels; column++)
            {
                int sourceValue = source[column];

                if (sourceValue > maximumSourceValue)
                {
                    throw new InvalidOperationException($"FreeType returned grayscale value {sourceValue} outside its declared range 0..{maximumSourceValue}.");
                }

                destination[column] = checked((byte)((sourceValue * 255) / maximumSourceValue));
            }
        }

        return coverage;
    }

    private static byte[] CopyMonochromeCoverage(nint buffer, int widthPixels, int heightPixels, int pitch)
    {
        int packedWidthBytes = checked((widthPixels + 7) / 8);
        ValidatePitch(pitch, packedWidthBytes);

        byte[] coverage = new byte[checked(widthPixels * heightPixels)];

        for (int row = 0; row < heightPixels; row++)
        {
            byte* source = GetLogicalRow(buffer, pitch, row, heightPixels);
            Span<byte> destination = coverage.AsSpan(checked(row * widthPixels), widthPixels);

            for (int column = 0; column < widthPixels; column++)
            {
                byte mask = (byte)(0x80 >> (column & 7));
                destination[column] = (source[column >> 3] & mask) != 0 ? byte.MaxValue : byte.MinValue;
            }
        }

        return coverage;
    }

    private static void ValidatePitch(int pitch, int minimumPitch)
    {
        if (pitch == 0)
        {
            throw new InvalidOperationException("FreeType returned a non-empty glyph bitmap with zero pitch.");
        }

        if (Math.Abs((long)pitch) < minimumPitch)
        {
            throw new InvalidOperationException($"FreeType glyph bitmap pitch {pitch} is smaller than the required {minimumPitch} bytes.");
        }
    }

    private static byte* GetLogicalRow(nint buffer, int pitch, int row, int heightPixels)
    {
        long rowOffset = pitch > 0 ? checked((long)row * pitch) : checked((heightPixels - 1 - row) * -(long)pitch);

        return (byte*)buffer + checked((nint)rowOffset);
    }
}
