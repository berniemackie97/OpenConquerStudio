using System.Buffers.Binary;
using System.Text;

namespace OpenConquer.Content.Mesh;

internal sealed class C3DatabaseArchive
{
    private const uint Magic = 0x474D4442;
    private const int HeaderLength = 16;
    private const int DescriptorLength = 84;
    private const int MaximumSize = 16 * 1024 * 1024;

    private readonly Dictionary<string, ReadOnlyMemory<byte>> _entries;

    private C3DatabaseArchive(byte[] data, Dictionary<string, ReadOnlyMemory<byte>> entries)
    {
        _entries = entries;
    }

    internal static C3DatabaseArchive Load(IClientContentSource source)
    {
        byte[] data = ContentReader.ReadRequiredBytes(source,
            "ini/c3.wdb", ContentLookupMode.LooseOnly, MaximumSize);
        return Parse(data);
    }

    internal static C3DatabaseArchive Parse(byte[] data)
    {
        if (data.Length < HeaderLength || data.Length > MaximumSize ||
            BinaryPrimitives.ReadUInt32LittleEndian(data) != Magic ||
            BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4)) != (uint)data.Length)
        {
            throw new InvalidDataException("Invalid C3 database archive header.");
        }

        uint tableOffset = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(8));
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(12));
        if (tableOffset < HeaderLength || count > 4096 ||
            (long)tableOffset + (long)count * DescriptorLength != data.Length)
        {
            throw new InvalidDataException("C3 database directory exceeds its archive.");
        }

        uint[] crypt = CreateCryptTable();
        Dictionary<string, ReadOnlyMemory<byte>> entries = new(StringComparer.OrdinalIgnoreCase);
        Span<byte> decoded = stackalloc byte[DescriptorLength];
        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<byte> bytes = data.AsSpan((int)tableOffset + i * DescriptorLength, DescriptorLength);
            DecryptDescriptor(bytes, decoded, crypt);
            uint hash = BinaryPrimitives.ReadUInt32LittleEndian(decoded);
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(decoded[4..]);
            uint storedLength = BinaryPrimitives.ReadUInt32LittleEndian(decoded[12..]);
            ReadOnlySpan<byte> nameBytes = decoded[20..];
            int terminator = nameBytes.IndexOf((byte)0);
            if (terminator <= 0 || offset < HeaderLength ||
                (long)offset + storedLength > tableOffset)
            {
                throw new InvalidDataException("C3 database entry is malformed.");
            }

            string name = Encoding.ASCII.GetString(nameBytes[..terminator]);
            if (HashPath(name, crypt) != hash)
            {
                throw new InvalidDataException("C3 database directory contains an invalid path hash.");
            }

            entries[name] = data.AsMemory((int)offset, (int)storedLength);
        }

        return new C3DatabaseArchive(data, entries);
    }

    internal ReadOnlyMemory<byte> ReadRequired(string path) =>
        _entries.TryGetValue(path, out ReadOnlyMemory<byte> bytes)
            ? bytes
            : throw new FileNotFoundException($"C3 database entry '{path}' was not found.");

    internal bool TryRead(string path, out ReadOnlyMemory<byte> bytes) =>
        _entries.TryGetValue(path, out bytes);

    private static void DecryptDescriptor(ReadOnlySpan<byte> input, Span<byte> output, uint[] table)
    {
        uint seed1 = 0xEEEEEEEE;
        uint seed2 = 0xEFFEAABB;
        for (int i = 0; i < DescriptorLength; i += 4)
        {
            seed1 = unchecked(seed1 + table[0x400 + (seed2 & 0xFF)]);
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(input[i..]) ^
                unchecked(seed1 + seed2);
            BinaryPrimitives.WriteUInt32LittleEndian(output[i..], value);
            seed2 = (seed2 >> 11) |
                unchecked((~seed2 << 21) + 0x11111111);
            seed1 = unchecked(seed1 * 33 + value + 3);
        }
    }

    private static uint HashPath(string path, uint[] table)
    {
        uint seed1 = 0x7FED7FED;
        uint seed2 = 0xEEEEEEEE;
        foreach (char letter in path)
        {
            uint upper = char.ToUpperInvariant(letter);
            if (upper > 127)
            {
                throw new InvalidDataException("C3 database path contains a non-ASCII character.");
            }

            seed1 = table[upper] ^ unchecked(seed1 + seed2);
            seed2 = unchecked(seed2 * 33 + upper + seed1 + 3);
        }

        return seed1;
    }

    private static uint[] CreateCryptTable()
    {
        uint[] table = new uint[1280];
        uint seed = 0x100001;
        for (int i = 0; i < 256; i++)
        {
            for (int column = 0; column < 5; column++)
            {
                seed = (seed * 125 + 3) % 0x2AAAAB;
                uint high = seed & 0xFFFF;
                seed = (seed * 125 + 3) % 0x2AAAAB;
                table[i + column * 256] = (high << 16) | (seed & 0xFFFF);
            }
        }

        return table;
    }
}
