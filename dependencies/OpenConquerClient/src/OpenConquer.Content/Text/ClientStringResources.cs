using System.Globalization;
using System.Text;

namespace OpenConquer.Content.Text;

/// <summary>
/// Resolves native numeric string resources without transcoding their encoded values.
/// </summary>
public sealed class ClientStringResources
{
    public const string RelativePath = "ini/StrRes.ini";
    public const int ClientLanguageId = 0x28BA;

    private const int NativeLineBufferLength = 0x400;
    private const int MaximumFileLength = 4 * 1024 * 1024;

    private readonly Dictionary<int, byte[]> _values;

    private ClientStringResources(Dictionary<int, byte[]> values)
    {
        _values = values;
    }

    public int Count => _values.Count;

    public bool UsesArabicLayout =>
        TryGetEncoded(ClientLanguageId, out ReadOnlyMemory<byte> language) && IsAsciiArabic(language.Span);

    public static ClientStringResources Load(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        byte[] encodedFile = ContentReader.ReadRequiredBytes(contentSource, RelativePath, ContentLookupMode.LooseOnly, MaximumFileLength);
        return Parse(encodedFile);
    }

    public static ClientStringResources Parse(ReadOnlySpan<byte> encodedFile)
    {
        if (encodedFile.Length > MaximumFileLength)
        {
            throw new InvalidDataException($"'{RelativePath}' exceeds the {MaximumFileLength}-byte safety limit.");
        }

        Dictionary<int, byte[]> values = [];
        Span<byte> line = stackalloc byte[NativeLineBufferLength - 1];
        int position = 0;

        while (position < encodedFile.Length)
        {
            int length = 0;

            while (length < line.Length && position < encodedFile.Length)
            {
                byte current = encodedFile[position++];

                if (current == (byte)'\r' && position < encodedFile.Length && encodedFile[position] == (byte)'\n')
                {
                    position++;
                    line[length++] = (byte)'\n';
                    break;
                }

                line[length++] = current;

                if (current == (byte)'\n')
                {
                    break;
                }
            }

            ParseLine(line[..length], values);
        }

        return new ClientStringResources(values);
    }

    public bool TryGetEncoded(int id, out ReadOnlyMemory<byte> value)
    {
        if (_values.TryGetValue(id, out byte[]? bytes))
        {
            value = bytes;
            return true;
        }

        value = default;
        return false;
    }

    public ReadOnlyMemory<byte> GetEncoded(int id)
    {
        return TryGetEncoded(id, out ReadOnlyMemory<byte> value)
            ? value
            : Encoding.ASCII.GetBytes($"SE:{unchecked((uint)id).ToString(CultureInfo.InvariantCulture)}");
    }

    private static void ParseLine(ReadOnlySpan<byte> line, Dictionary<int, byte[]> values)
    {
        int terminator = line.IndexOf((byte)0);

        if (terminator >= 0)
        {
            line = line[..terminator];
        }

        if (!line.IsEmpty && line[^1] == (byte)'\n')
        {
            line = line[..^1];
        }

        if (!line.IsEmpty && line[^1] == (byte)'\r')
        {
            line = line[..^1];
        }

        if (line.IsEmpty || line[0] == (byte)';')
        {
            return;
        }

        int separator = line.IndexOf((byte)'=');

        if (separator < 0)
        {
            return;
        }

        int id = ParseIdentifier(line[..separator]);
        values[id] = line[(separator + 1)..].ToArray();
    }

    private static int ParseIdentifier(ReadOnlySpan<byte> encoded)
    {
        int index = 0;

        while (index < encoded.Length && encoded[index] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or 0x0B or 0x0C)
        {
            index++;
        }

        bool negative = false;

        if (index < encoded.Length && encoded[index] is (byte)'+' or (byte)'-')
        {
            negative = encoded[index] == (byte)'-';
            index++;
        }

        long value = 0;
        bool hasDigits = false;
        long limit = negative ? 2147483648L : int.MaxValue;

        while (index < encoded.Length && encoded[index] is >= (byte)'0' and <= (byte)'9')
        {
            hasDigits = true;
            int digit = encoded[index] - (byte)'0';

            if (value > (limit - digit) / 10)
            {
                throw new InvalidDataException($"'{RelativePath}' contains an identifier outside the supported 32-bit integer range.");
            }

            value = value * 10 + digit;
            index++;
        }

        return !hasDigits ? 0 : negative ? checked((int)-value) : checked((int)value);
    }

    private static bool IsAsciiArabic(ReadOnlySpan<byte> value)
    {
        ReadOnlySpan<byte> arabic = "Arabic"u8;

        if (value.Length != arabic.Length)
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            byte character = value[index];
            byte expected = arabic[index];

            if (character is >= (byte)'A' and <= (byte)'Z')
            {
                character = (byte)(character + 32);
            }

            if (expected is >= (byte)'A' and <= (byte)'Z')
            {
                expected = (byte)(expected + 32);
            }

            if (character != expected)
            {
                return false;
            }
        }

        return true;
    }
}
