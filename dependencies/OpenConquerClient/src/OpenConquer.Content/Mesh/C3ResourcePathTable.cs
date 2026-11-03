using System.Buffers.Binary;
using System.Text;

namespace OpenConquer.Content.Mesh;

internal static class C3ResourcePathTable
{
    internal static Dictionary<uint, string> Read(ReadOnlyMemory<byte> payload)
    {
        ReadOnlySpan<byte> bytes = payload.Span;
        if (bytes.Length < 8 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x42445352)
        {
            throw new InvalidDataException("Invalid C3 resource path index.");
        }

        uint count = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        long dataStart = 8L + count * 8L;
        if (count > 1_000_000 || dataStart > bytes.Length)
        {
            throw new InvalidDataException("C3 resource index exceeds its payload.");
        }

        Dictionary<uint, string> paths = new((int)count);
        for (int i = 0; i < count; i++)
        {
            int entry = 8 + i * 8;
            uint key = BinaryPrimitives.ReadUInt32LittleEndian(bytes[entry..]);
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(entry + 4)..]);
            paths[key] = ReadPath(bytes, offset, dataStart);
        }

        return paths;
    }

    internal static bool TryFindDualKey(ReadOnlyMemory<byte> payload, ulong key, out string path)
    {
        ReadOnlySpan<byte> bytes = payload.Span;
        if (bytes.Length < 8 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x42445352)
        {
            throw new InvalidDataException("Invalid C3 motion path index.");
        }

        uint count = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        long dataStart = 8L + count * 12L;
        if (count > 1_000_000 || dataStart > bytes.Length)
        {
            throw new InvalidDataException("C3 motion index exceeds its payload.");
        }

        uint? foundOffset = null;
        for (int i = 0; i < count; i++)
        {
            int entry = 8 + i * 12;
            uint low = BinaryPrimitives.ReadUInt32LittleEndian(bytes[entry..]);
            uint high = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(entry + 4)..]);
            if (((ulong)high << 32 | low) == key)
            {
                foundOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(entry + 8)..]);
            }
        }

        if (foundOffset is { } offset)
        {
            path = ReadPath(bytes, offset, dataStart);
            return true;
        }

        path = string.Empty;
        return false;
    }

    private static string ReadPath(ReadOnlySpan<byte> bytes, uint offset, long dataStart)
    {
        if (offset < dataStart || offset >= bytes.Length)
        {
            throw new InvalidDataException("C3 resource path offset is outside its payload.");
        }

        ReadOnlySpan<byte> tail = bytes[(int)offset..];
        int length = tail.IndexOf((byte)0);
        if (length <= 0 || length > 255)
        {
            throw new InvalidDataException("C3 resource path has no bounded terminator.");
        }

        return Encoding.ASCII.GetString(tail[..length]);
    }
}
