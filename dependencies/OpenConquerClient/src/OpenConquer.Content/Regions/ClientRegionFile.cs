using System.Buffers.Binary;

namespace OpenConquer.Content.Regions;

public readonly record struct ClientRegionRectangle(int Left, int Top, int Right, int Bottom)
{
    public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;
}

/// <summary>
/// Represents an untransformed Win32 RGNDATA region from retail client content.
/// </summary>
public sealed class ClientRegionFile
{
    private const int MaximumFileLength = 256 * 1024;
    private const int HeaderSize = 32;
    private const int RectangleSize = 16;

    private readonly ClientRegionRectangle[] _rectangles;
    private readonly IReadOnlyList<ClientRegionRectangle> _readOnlyRectangles;

    private ClientRegionFile(ClientRegionRectangle[] rectangles, ClientRegionRectangle bounds)
    {
        _rectangles = rectangles;
        _readOnlyRectangles = Array.AsReadOnly(rectangles);
        Bounds = bounds;
    }

    public int RectangleCount => _rectangles.Length;

    public ClientRegionRectangle Bounds
    {
        get;
    }
    public IReadOnlyList<ClientRegionRectangle> Rectangles => _readOnlyRectangles;

    public static ClientRegionFile Load(IClientContentSource contentSource, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        byte[] encoded = ContentReader.ReadRequiredBytes(contentSource, relativePath, ContentLookupMode.LooseOnly, MaximumFileLength);
        return Parse(encoded, relativePath);
    }

    public static ClientRegionFile Parse(ReadOnlySpan<byte> encodedFile, string contentPath = "client region")
    {
        if (encodedFile.Length < sizeof(uint) + HeaderSize || encodedFile.Length > MaximumFileLength)
        {
            throw Invalid(contentPath, "invalid file length");
        }

        uint declaredLength = BinaryPrimitives.ReadUInt32LittleEndian(encodedFile);
        ReadOnlySpan<byte> payload = encodedFile[sizeof(uint)..];

        if (declaredLength != (uint)payload.Length)
        {
            throw Invalid(contentPath, "declared payload length does not match the file");
        }

        uint headerSize = ReadUInt32(payload, 0);
        uint type = ReadUInt32(payload, 4);
        uint count = ReadUInt32(payload, 8);
        uint rectangleBytes = ReadUInt32(payload, 12);

        if (headerSize != HeaderSize || type != 1 || count == 0 ||
            (ulong)count * RectangleSize != rectangleBytes ||
            HeaderSize + (ulong)rectangleBytes != (ulong)payload.Length ||
            count > int.MaxValue)
        {
            throw Invalid(contentPath, "invalid RGNDATA header");
        }

        ClientRegionRectangle bounds = ReadRectangle(payload, 16);
        ClientRegionRectangle[] rectangles = new ClientRegionRectangle[(int)count];

        int left = int.MaxValue;
        int top = int.MaxValue;
        int right = int.MinValue;
        int bottom = int.MinValue;

        for (int index = 0; index < rectangles.Length; index++)
        {
            ClientRegionRectangle rectangle = ReadRectangle(payload, HeaderSize + index * RectangleSize);

            if (rectangle.Right <= rectangle.Left || rectangle.Bottom <= rectangle.Top)
            {
                throw Invalid(contentPath, $"rectangle {index} has nonpositive dimensions");
            }

            rectangles[index] = rectangle;
            left = Math.Min(left, rectangle.Left);
            top = Math.Min(top, rectangle.Top);
            right = Math.Max(right, rectangle.Right);
            bottom = Math.Max(bottom, rectangle.Bottom);
        }

        if (bounds != new ClientRegionRectangle(left, top, right, bottom))
        {
            throw Invalid(contentPath, "bounding rectangle does not match the rectangle union");
        }

        return new ClientRegionFile(rectangles, bounds);
    }

    public bool Contains(int x, int y)
    {
        if (!Bounds.Contains(x, y))
        {
            return false;
        }

        foreach (ClientRegionRectangle rectangle in _rectangles)
        {
            if (rectangle.Contains(x, y))
            {
                return true;
            }
        }

        return false;
    }

    private static ClientRegionRectangle ReadRectangle(ReadOnlySpan<byte> bytes, int offset)
    {
        return new ClientRegionRectangle(
            BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..]),
            BinaryPrimitives.ReadInt32LittleEndian(bytes[(offset + 4)..]),
            BinaryPrimitives.ReadInt32LittleEndian(bytes[(offset + 8)..]),
            BinaryPrimitives.ReadInt32LittleEndian(bytes[(offset + 12)..]));
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    }

    private static InvalidDataException Invalid(string contentPath, string reason)
    {
        return new InvalidDataException($"Region '{contentPath}' is invalid: {reason}.");
    }
}
