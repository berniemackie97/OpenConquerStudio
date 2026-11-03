using System.Buffers.Binary;

namespace OpenConquer.Content.Images;

internal static class WindowsBitmapReader
{
    private const int BitmapFileHeaderLength = 14;
    private const int MinimumInfoHeaderLength = 40;
    private const int MaximumDimension = 16_384;
    private const int MaximumDecodedLength = 256 * 1024 * 1024;

    public static RgbaImage Decode24Bit(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < BitmapFileHeaderLength + MinimumInfoHeaderLength || payload[0] != (byte)'B' || payload[1] != (byte)'M')
        {
            throw new InvalidDataException("The startup image is not a Windows BMP file.");
        }

        uint pixelOffset = BinaryPrimitives.ReadUInt32LittleEndian(payload[10..]);
        uint infoHeaderLength = BinaryPrimitives.ReadUInt32LittleEndian(payload[14..]);

        if (infoHeaderLength < MinimumInfoHeaderLength)
        {
            throw new InvalidDataException("The startup BMP uses an unsupported information header.");
        }

        long minimumPixelOffset = checked(BitmapFileHeaderLength + infoHeaderLength);

        if (pixelOffset < minimumPixelOffset || pixelOffset > payload.Length)
        {
            throw new InvalidDataException("The startup BMP pixel offset is outside the file.");
        }

        int width = BinaryPrimitives.ReadInt32LittleEndian(payload[18..]);
        int signedHeight = BinaryPrimitives.ReadInt32LittleEndian(payload[22..]);
        ushort planeCount = BinaryPrimitives.ReadUInt16LittleEndian(payload[26..]);
        ushort bitsPerPixel = BinaryPrimitives.ReadUInt16LittleEndian(payload[28..]);
        uint compression = BinaryPrimitives.ReadUInt32LittleEndian(payload[30..]);

        if (width is <= 0 or > MaximumDimension || signedHeight is 0 or int.MinValue || Math.Abs(signedHeight) > MaximumDimension)
        {
            throw new InvalidDataException("The startup BMP dimensions are invalid or exceed the supported limit.");
        }

        if (planeCount != 1 || bitsPerPixel != 24 || compression != 0)
        {
            throw new InvalidDataException("The startup BMP must be an uncompressed 24-bit Windows bitmap.");
        }

        int height = Math.Abs(signedHeight);
        int sourceRowLength = checked((width * 3 + 3) & ~3);
        int sourceLength = checked(sourceRowLength * height);
        int sourceEnd = checked((int)pixelOffset + sourceLength);

        if (sourceEnd > payload.Length)
        {
            throw new InvalidDataException("The startup BMP pixel payload is truncated.");
        }

        int decodedLength = checked(width * height * 4);

        if (decodedLength > MaximumDecodedLength)
        {
            throw new InvalidDataException("The decoded startup BMP exceeds the supported memory limit.");
        }

        byte[] pixels = GC.AllocateUninitializedArray<byte>(decodedLength);
        bool topDown = signedHeight < 0;

        for (int destinationY = 0; destinationY < height; destinationY++)
        {
            int sourceY = topDown ? destinationY : height - destinationY - 1;
            int sourceRowOffset = checked((int)pixelOffset + sourceY * sourceRowLength);
            int destinationRowOffset = destinationY * width * 4;

            for (int x = 0; x < width; x++)
            {
                int sourcePixelOffset = sourceRowOffset + x * 3;
                int destinationPixelOffset = destinationRowOffset + x * 4;

                pixels[destinationPixelOffset] = payload[sourcePixelOffset + 2];
                pixels[destinationPixelOffset + 1] = payload[sourcePixelOffset + 1];
                pixels[destinationPixelOffset + 2] = payload[sourcePixelOffset];
                pixels[destinationPixelOffset + 3] = byte.MaxValue;
            }
        }

        return new RgbaImage(width, height, pixels);
    }
}
