using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;

namespace OpenConquer.Content.Magic;

public sealed class MagicTypeFile
{
    public const string RelativePath = "ini/MagicType.dat";

    private const int MaximumFileLength = 1024 * 1024;
    private const int SourceFieldCount = 48;
    private const int CipherKeyLength = 128;
    private const uint CipherSeed = 0x2537;

    private readonly Dictionary<uint, MagicTypeRecord> _records;

    private MagicTypeFile(Dictionary<uint, MagicTypeRecord> records)
    {
        _records = records;
    }

    public int Count => _records.Count;

    public static MagicTypeFile Load(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        byte[] encoded = ContentReader.ReadRequiredBytes(contentSource, RelativePath, ContentLookupMode.LooseOnly, MaximumFileLength);
        return Parse(encoded);
    }

    public static MagicTypeFile Parse(ReadOnlySpan<byte> encodedFile)
    {
        if (encodedFile.IsEmpty || encodedFile.Length > MaximumFileLength)
        {
            throw new InvalidDataException($"'{RelativePath}' must contain between 1 and {MaximumFileLength} bytes.");
        }

        byte[] decoded = encodedFile.ToArray();
        DecodeInPlace(decoded);

        Dictionary<uint, MagicTypeRecord> records = [];
        int position = 0;
        int lineNumber = 0;

        while (position < decoded.Length)
        {
            lineNumber++;

            ReadOnlySpan<byte> remaining = decoded.AsSpan(position);
            int lineEnd = remaining.IndexOf((byte)'\n');
            ReadOnlySpan<byte> line = lineEnd >= 0 ? remaining[..lineEnd] : remaining;

            if (!line.IsEmpty && line[^1] == (byte)'\r')
            {
                line = line[..^1];
            }

            if (line.IsEmpty)
            {
                throw Invalid(lineNumber, "empty record");
            }

            MagicTypeRecord record = ParseRecord(line, lineNumber);
            uint key = unchecked(record.Type * 10 + record.Level);

            if (!records.TryAdd(key, record))
            {
                throw Invalid(lineNumber, $"duplicate native lookup key {key}");
            }

            position += lineEnd >= 0 ? lineEnd + 1 : remaining.Length;
        }

        return new MagicTypeFile(records);
    }

    public bool TryGet(uint type, uint level, [NotNullWhen(true)] out MagicTypeRecord? record)
    {
        uint key = unchecked(type * 10 + level);
        return _records.TryGetValue(key, out record);
    }

    internal static void DecodeInPlace(Span<byte> bytes)
    {
        Span<byte> key = stackalloc byte[CipherKeyLength];
        uint state = CipherSeed;

        for (int index = 0; index < key.Length; index++)
        {
            state = unchecked(state * 214013 + 2531011);
            key[index] = (byte)(state >> 16);
        }

        for (int index = 0; index < bytes.Length; index++)
        {
            byte value = (byte)(bytes[index] ^ key[index % CipherKeyLength]);
            int rotation = index & 7;
            bytes[index] = unchecked((byte)((value >> rotation) | (value << (8 - rotation))));
        }
    }

    private static MagicTypeRecord ParseRecord(ReadOnlySpan<byte> line, int lineNumber)
    {
        Span<uint> fields = stackalloc uint[SourceFieldCount];
        int position = 0;

        for (int index = 0; index < fields.Length; index++)
        {
            int separator = line[position..].IndexOf("@@"u8);

            if (separator < 0)
            {
                throw Invalid(lineNumber, $"missing delimiter after field {index}");
            }

            ReadOnlySpan<byte> token = line.Slice(position, separator);

            // Field 3 is the magic name, not a number.
            if (index != 3)
            {
                if (!Utf8Parser.TryParse(token, out int value, out int consumed) || consumed != token.Length)
                {
                    throw Invalid(lineNumber, $"invalid numeric field {index}");
                }

                fields[index] = unchecked((uint)value);
            }

            position += separator + 2;
        }

        if (position != line.Length)
        {
            throw Invalid(lineNumber, "unexpected data after the final field");
        }

        // Original 0x69C19E assignments, verified by the unique-token x86 replay.
        ReadOnlySpan<uint> nativeWords =
        [
            fields[23], fields[47], fields[4], fields[15],
            fields[13], fields[18], fields[8], fields[20],
            fields[9], fields[32], fields[10], fields[14],
            fields[42], fields[41], fields[29], fields[22],
            fields[1]
        ];

        return new MagicTypeRecord(nativeWords);
    }

    private static InvalidDataException Invalid(int lineNumber, string reason)
    {
        return new InvalidDataException($"'{RelativePath}' line {lineNumber}: {reason}.");
    }
}
