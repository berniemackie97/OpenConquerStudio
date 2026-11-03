using System.Buffers.Binary;

namespace OpenConquer.Content.Images;

public static class TargaImageReader
{
    private const int HeaderLength = 18;
    private const int MaximumDimension = 16_384;
    private const int MaximumDecodedLength = 256 * 1024 * 1024;
    private const byte RleTrueColorImageType = 10;
    private const byte RgbaPixelDepth = 32;
    private const byte SupportedImageDescriptor = 0x08;

    public static RgbaImage DecodeRle32Bit(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < HeaderLength)
        {
            throw new InvalidDataException("The TGA image header is truncated.");
        }

        byte imageIdLength = payload[0];
        byte colorMapType = payload[1];
        byte imageType = payload[2];
        ushort colorMapFirstEntry = BinaryPrimitives.ReadUInt16LittleEndian(payload[3..]);
        ushort colorMapLength = BinaryPrimitives.ReadUInt16LittleEndian(payload[5..]);
        byte colorMapEntrySize = payload[7];
        ushort xOrigin = BinaryPrimitives.ReadUInt16LittleEndian(payload[8..]);
        ushort yOrigin = BinaryPrimitives.ReadUInt16LittleEndian(payload[10..]);
        int width = BinaryPrimitives.ReadUInt16LittleEndian(payload[12..]);
        int height = BinaryPrimitives.ReadUInt16LittleEndian(payload[14..]);
        byte pixelDepth = payload[16];
        byte imageDescriptor = payload[17];

        if (imageIdLength != 0)
        {
            throw new InvalidDataException("The TGA image must not contain an image ID.");
        }

        if (colorMapType != 0 || colorMapFirstEntry != 0 || colorMapLength != 0 || colorMapEntrySize != 0)
        {
            throw new InvalidDataException("The TGA image must not contain a color map.");
        }

        if (imageType != RleTrueColorImageType)
        {
            throw new InvalidDataException($"The TGA image type is {imageType}; expected RLE true-color type {RleTrueColorImageType}.");
        }

        if (xOrigin != 0 || yOrigin != 0)
        {
            throw new InvalidDataException("The TGA image must use a zero image origin.");
        }

        if (width is <= 0 or > MaximumDimension || height is <= 0 or > MaximumDimension)
        {
            throw new InvalidDataException("The TGA dimensions are invalid or exceed the supported limit.");
        }

        if (pixelDepth != RgbaPixelDepth)
        {
            throw new InvalidDataException($"The TGA pixel depth is {pixelDepth}; expected {RgbaPixelDepth} bits.");
        }

        if (imageDescriptor != SupportedImageDescriptor)
        {
            throw new InvalidDataException($"The TGA image descriptor is 0x{imageDescriptor:X2}; expected 0x{SupportedImageDescriptor:X2}.");
        }

        int pixelCount = checked(width * height);
        long decodedLength = checked((long)pixelCount * 4);

        if (decodedLength > MaximumDecodedLength)
        {
            throw new InvalidDataException("The decoded TGA exceeds the supported memory limit.");
        }

        byte[] pixels = GC.AllocateUninitializedArray<byte>((int)decodedLength);
        int sourceOffset = HeaderLength;
        int sourcePixelIndex = 0;

        while (sourcePixelIndex < pixelCount)
        {
            if (sourceOffset >= payload.Length)
            {
                throw new InvalidDataException("The TGA RLE payload is truncated before all pixels were decoded.");
            }

            byte packetHeader = payload[sourceOffset++];
            int packetPixelCount = (packetHeader & 0x7F) + 1;

            if (packetPixelCount > pixelCount - sourcePixelIndex)
            {
                throw new InvalidDataException("The TGA RLE packet exceeds the declared image dimensions.");
            }

            if ((packetHeader & 0x80) != 0)
            {
                if (payload.Length - sourceOffset < 4)
                {
                    throw new InvalidDataException("The TGA RLE packet contains a truncated pixel.");
                }

                ReadOnlySpan<byte> sourcePixel = payload.Slice(sourceOffset, 4);
                sourceOffset += 4;

                for (int index = 0; index < packetPixelCount; index++)
                {
                    WritePixel(sourcePixel, pixels, sourcePixelIndex++, width, height);
                }

                continue;
            }

            int packetLength = packetPixelCount * 4;

            if (payload.Length - sourceOffset < packetLength)
            {
                throw new InvalidDataException("The TGA raw packet is truncated.");
            }

            for (int index = 0; index < packetPixelCount; index++)
            {
                WritePixel(payload.Slice(sourceOffset, 4), pixels, sourcePixelIndex++, width, height);
                sourceOffset += 4;
            }
        }

        return new RgbaImage(width, height, pixels);
    }

    private static void WritePixel(ReadOnlySpan<byte> sourcePixel, byte[] destination, int sourcePixelIndex, int width, int height)
    {
        int sourceX = sourcePixelIndex % width;
        int sourceY = sourcePixelIndex / width;
        int destinationY = height - sourceY - 1;
        int destinationOffset = checked((destinationY * width + sourceX) * 4);

        destination[destinationOffset] = sourcePixel[2];
        destination[destinationOffset + 1] = sourcePixel[1];
        destination[destinationOffset + 2] = sourcePixel[0];
        destination[destinationOffset + 3] = sourcePixel[3];
    }
}
