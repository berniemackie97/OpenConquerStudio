using System.Buffers.Binary;
using System.Security.Cryptography;

namespace OpenConquerStudio.Runtime;

/// <summary>Bounded texture transport. The digest detects corruption; it does not authenticate a publisher.</summary>
public static class TextureCodec
{
    private const int HeaderLength = 68;

    public static void Write(Stream output, Guid assetId, TextureData texture)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(texture);
        if (assetId == Guid.Empty)
        {
            throw new InvalidDataException("A compiled texture requires a stable asset ID.");
        }

        Span<byte> header = stackalloc byte[HeaderLength];
        "OCTX"u8.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], 1);
        assetId.TryWriteBytes(header[8..24], bigEndian: true, out _);
        BinaryPrimitives.WriteInt32LittleEndian(header[24..], texture.Width);
        BinaryPrimitives.WriteInt32LittleEndian(header[28..], texture.Height);
        BinaryPrimitives.WriteInt32LittleEndian(header[32..], texture.Pixels.Length);
        SHA256.HashData(texture.Pixels.Span, header[36..]);
        output.Write(header);
        output.Write(texture.Pixels.Span);
    }

    public static (Guid AssetId, TextureData Texture) Read(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Span<byte> header = stackalloc byte[HeaderLength];
        input.ReadExactly(header);
        if (!header[..4].SequenceEqual("OCTX"u8) || BinaryPrimitives.ReadInt32LittleEndian(header[4..]) != 1)
        {
            throw new InvalidDataException("Unsupported compiled texture header or version.");
        }

        Guid id = new(header[8..24], bigEndian: true);
        int width = BinaryPrimitives.ReadInt32LittleEndian(header[24..]);
        int height = BinaryPrimitives.ReadInt32LittleEndian(header[28..]);
        int length = TextureData.GetByteLength(width, height);
        if (id == Guid.Empty || BinaryPrimitives.ReadInt32LittleEndian(header[32..]) != length)
        {
            throw new InvalidDataException("Invalid compiled texture identity or pixel length.");
        }

        byte[] pixels = new byte[length];
        input.ReadExactly(pixels);
        if (input.ReadByte() != -1 || !SHA256.HashData(pixels).AsSpan().SequenceEqual(header[36..]))
        {
            throw new InvalidDataException("Compiled texture digest or file coverage is invalid.");
        }

        return (id, new TextureData(width, height, pixels));
    }
}
