using System.Buffers.Binary;
using OpenConquer.Content.Images;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio.Authoring;

public static class TextureImporter
{
    public static (TextureData Texture, string Format, string Digest) Read(string path, CancellationToken cancellationToken)
    {
        string format = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".tga" => "tga",
            ".dds" => "dds",
            _ => throw new InvalidDataException("Select a supported .tga or .dds texture."),
        };
        cancellationToken.ThrowIfCancellationRequested();
        byte[] bytes = ProjectFiles.Read(path, 16 * 1024 * 1024);
        return Decode(bytes, format, cancellationToken);
    }

    public static (TextureData Texture, string Format, string Digest) Decode(byte[] bytes, string format, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length > 16 * 1024 * 1024 || format is not ("tga" or "dds"))
        {
            throw new InvalidDataException("Unsupported image format or encoded image exceeds 16 MiB.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        ReadOnlySpan<byte> header = bytes;
        // Apply the Studio allocation budget before calling the authoritative decoders.
        if (format == "tga" && header.Length >= 18)
        {
            TextureData.GetByteLength(BinaryPrimitives.ReadUInt16LittleEndian(header[12..]), BinaryPrimitives.ReadUInt16LittleEndian(header[14..]));
        }
        else if (format == "dds" && header.Length >= 128)
        {
            TextureData.GetByteLength(BinaryPrimitives.ReadInt32LittleEndian(header[16..]), BinaryPrimitives.ReadInt32LittleEndian(header[12..]));
        }

        RgbaImage decoded = format == "tga"
            ? TargaImageReader.DecodeRle32Bit(bytes)
            : DdsImageReader.Decode(bytes);
        cancellationToken.ThrowIfCancellationRequested();
        return (new TextureData(decoded.Width, decoded.Height, decoded.Pixels.Span), format, ProjectFiles.Hash(bytes));
    }
}
