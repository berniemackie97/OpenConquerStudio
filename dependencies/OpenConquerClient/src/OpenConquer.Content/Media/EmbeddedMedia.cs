using System.IO.Compression;
using System.Security.Cryptography;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Media;

/// <summary>An embedded, Brotli-compressed straight-alpha RGBA image and the SHA-256 of its decoded pixels.</summary>
internal readonly record struct EmbeddedImage(string Path, int Width, int Height, string Sha256);

/// <summary>
/// Reads media converted offline from the retail client and embedded under <c>Assets/</c>.
/// Every resource must decode to exactly its declared length and SHA-256; anything else is corrupt.
/// </summary>
internal static class EmbeddedMedia
{
    public static RgbaImage Load(EmbeddedImage image) =>
        new(image.Width, image.Height, Read(image.Path, checked(image.Width * image.Height * 4), image.Sha256, compressed: true));

    public static byte[] Read(string path, int length, string sha256, bool compressed)
    {
        using Stream resource = typeof(EmbeddedMedia).Assembly.GetManifestResourceStream("Assets/" + path)
            ?? throw new InvalidDataException($"Embedded media '{path}' is missing.");
        using Stream content = compressed ? new BrotliStream(resource, CompressionMode.Decompress) : resource;
        byte[] data = GC.AllocateUninitializedArray<byte>(length);

        try
        {
            content.ReadExactly(data);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException($"Embedded media '{path}' is truncated.", exception);
        }

        if (content.ReadByte() != -1)
        {
            throw new InvalidDataException($"Embedded media '{path}' is longer than declared.");
        }

        if (!SHA256.HashData(data).AsSpan().SequenceEqual(Convert.FromHexString(sha256)))
        {
            throw new InvalidDataException($"Embedded media '{path}' failed integrity verification.");
        }

        return data;
    }
}
