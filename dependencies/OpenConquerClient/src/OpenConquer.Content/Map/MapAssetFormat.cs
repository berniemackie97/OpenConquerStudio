using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace OpenConquer.Content.Map;

internal enum MapSection : uint
{
    References = 1,
    Cells = 2,
    Objects = 3,
    Layers = 4,
}

internal readonly record struct MapSectionEntry(long Offset, long Length, byte[] Digest);

internal static class MapAssetFormat
{
    internal const uint Version = 1;
    internal const int SectionCount = 4;
    internal const int HeaderSize = 16 + SectionCount * (4 + 8 + 8 + 32);
    internal const int MaxBytes = 32 * 1024 * 1024;
    internal const int MaxCells = 4 * 1024 * 1024;
    internal const int MaxObjects = 100_000;
    internal const int MaxLayers = 4096;
    internal const int MaxEntries = 100_000;
    internal const int MaxReferences = 65536;
    internal static readonly UTF8Encoding Utf8 = new(false, true);

    internal static void WriteHeader(BinaryWriter writer, uint mapId, IReadOnlyList<MapSectionEntry> entries)
    {
        writer.Write("OCMP"u8);
        writer.Write(Version);
        writer.Write(mapId);
        writer.Write((uint)SectionCount);
        for (int i = 0; i < SectionCount; i++)
        {
            writer.Write((uint)i + 1);
            writer.Write(entries[i].Offset);
            writer.Write(entries[i].Length);
            writer.Write(entries[i].Digest);
        }
    }

    internal static (uint MapId, MapSectionEntry[] Entries) ReadHeader(Stream stream)
    {
        if (!stream.CanRead || !stream.CanSeek || stream.Length < HeaderSize || stream.Length > MaxBytes)
        {
            throw new InvalidDataException("Map asset stream is not a supported seekable map.");
        }

        stream.Position = 0;
        using BinaryReader reader = new(stream, Utf8, leaveOpen: true);
        bool hasMagic = reader.ReadBytes(4).AsSpan().SequenceEqual("OCMP"u8);
        uint version = reader.ReadUInt32();
        uint mapId = reader.ReadUInt32();
        uint sectionCount = reader.ReadUInt32();
        if (!hasMagic || version != Version || mapId == 0 || sectionCount != SectionCount)
        {
            throw new InvalidDataException("Map asset header or schema version is unsupported.");
        }

        MapSectionEntry[] sections = new MapSectionEntry[SectionCount];
        long next = HeaderSize;
        for (int i = 0; i < sections.Length; i++)
        {
            if (reader.ReadUInt32() != (uint)i + 1)
            {
                throw new InvalidDataException("Map asset section directory is not canonical.");
            }

            long offset = reader.ReadInt64();
            long length = reader.ReadInt64();
            byte[] digest = reader.ReadBytes(32);
            if (digest.Length != 32 || offset != next || length <= 0 || length > stream.Length - next)
            {
                throw new InvalidDataException("Map asset section boundary is invalid.");
            }

            sections[i] = new MapSectionEntry(offset, length, digest);
            next += length;
        }

        if (next != stream.Length)
        {
            throw new InvalidDataException("Map asset contains undeclared or trailing bytes.");
        }

        return (mapId, sections);
    }

    internal static byte[] HashSection(Stream stream, long offset, long length)
    {
        stream.Position = offset;
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            long remaining = length;
            while (remaining > 0)
            {
                int count = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                if (count == 0)
                {
                    throw new InvalidDataException("Map asset section is truncated.");
                }

                hash.AppendData(buffer.AsSpan(0, count));
                remaining -= count;
            }

            return hash.GetHashAndReset();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    internal static void WriteString(BinaryWriter writer, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.IsNormalized(NormalizationForm.FormC) ||
            value.Any(char.IsControl))
        {
            throw new InvalidDataException("Map asset string is not canonical.");
        }

        byte[] bytes = Utf8.GetBytes(value);
        if (bytes.Length > 1024)
        {
            throw new InvalidDataException("Map asset string exceeds its limit.");
        }

        writer.Write((ushort)bytes.Length);
        writer.Write(bytes);
    }

    internal static string ReadString(BinaryReader reader, long end)
    {
        Require(reader, end, 2);
        int size = reader.ReadUInt16();
        if (size is 0 or > 1024)
        {
            throw new InvalidDataException("Map asset string length is invalid.");
        }

        Require(reader, end, size);
        string value = Utf8.GetString(reader.ReadBytes(size));
        if (!value.IsNormalized(NormalizationForm.FormC) || value.Any(char.IsControl))
        {
            throw new InvalidDataException("Map asset string is not canonical.");
        }
        return value;
    }

    internal static int ReadCount(BinaryReader reader, long end, int maximum, int minBytes, string name)
    {
        Require(reader, end, 4);
        int count = reader.ReadInt32();
        if (count < 0 || count > maximum || (long)count * minBytes > end - reader.BaseStream.Position)
        {
            throw new InvalidDataException($"Map asset {name} count exceeds the declared section.");
        }

        return count;
    }

    internal static void Require(BinaryReader reader, long end, long length)
    {
        if (length < 0 || length > end - reader.BaseStream.Position)
        {
            throw new InvalidDataException("Map asset section is truncated.");
        }
    }

    internal static int CellCount(int width, int height)
    {
        long count = (long)width * height;
        if (width <= 0 || height <= 0 || count > MaxCells)
        {
            throw new InvalidDataException("Map dimensions exceed the supported cell limit.");
        }

        return (int)count;
    }

    internal static void CheckCount(int count, int max, string name)
    {
        if (count < 0 || count > max)
        {
            throw new InvalidDataException($"Map asset {name} count exceeds its limit.");
        }
    }
}
