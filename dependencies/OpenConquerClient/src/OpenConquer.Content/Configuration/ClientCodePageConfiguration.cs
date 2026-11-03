namespace OpenConquer.Content.Configuration;

/// <summary>
/// Resolves the client codepage configuration used by the text pipeline.
/// </summary>
public sealed class ClientCodePageConfiguration
{
    public const string RelativePath = "ini/CodePage.ini";
    public const int NativeUnsetCodePage = 0;
    public const int DefaultEffectiveCodePage = 936;

    private const int MaximumFirstLineLength = 256;

    private ClientCodePageConfiguration(int configuredCodePage)
    {
        ConfiguredCodePage = configuredCodePage;
    }

    /// <summary>
    /// Gets the value the native client would store in its code-page global.
    /// Zero represents the native CP_ACP default.
    /// </summary>
    public int ConfiguredCodePage
    {
        get;
    }

    /// <summary>
    /// The code page used to decode text. When none is configured this is CP936, so every platform decodes the
    /// same way.
    /// </summary>
    public int EffectiveCodePage => ConfiguredCodePage == NativeUnsetCodePage
        ? DefaultEffectiveCodePage
        : ConfiguredCodePage;

    public static ClientCodePageConfiguration Load(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        if (!contentSource.TryOpenRead(RelativePath, ContentLookupMode.LooseOnly, out Stream? stream))
        {
            return new ClientCodePageConfiguration(NativeUnsetCodePage);
        }

        using (stream)
        {
            return new ClientCodePageConfiguration(ReadConfiguredCodePage(stream));
        }
    }

    internal static int ParseConfiguredCodePage(ReadOnlySpan<byte> line)
    {
        int index = 0;

        while (index < line.Length && IsAsciiWhitespace(line[index]))
        {
            index++;
        }

        bool negative = false;

        if (index < line.Length && line[index] is (byte)'+' or (byte)'-')
        {
            negative = line[index] == (byte)'-';
            index++;
        }

        int digitStart = index;
        long value = 0;

        while (index < line.Length && line[index] is >= (byte)'0' and <= (byte)'9')
        {
            value = (value * 10) + (line[index] - (byte)'0');

            if ((!negative && value > int.MaxValue) || (negative && value > 1L + int.MaxValue))
            {
                throw new InvalidDataException($"'{RelativePath}' contains a code-page value outside the supported 32-bit integer range.");
            }

            index++;
        }

        if (index == digitStart)
        {
            return NativeUnsetCodePage;
        }

        return negative ? checked((int)-value) : checked((int)value);
    }

    private static int ReadConfiguredCodePage(Stream stream)
    {
        if (!stream.CanRead)
        {
            throw new ArgumentException("Content stream must be readable.", nameof(stream));
        }

        Span<byte> line = stackalloc byte[MaximumFirstLineLength];
        int length = 0;

        while (true)
        {
            int value = stream.ReadByte();

            if (value is -1 or '\n')
            {
                break;
            }

            if (length == line.Length)
            {
                throw new InvalidDataException($"'{RelativePath}' first line exceeds the {MaximumFirstLineLength}-byte safety limit.");
            }

            line[length++] = (byte)value;
        }

        if (length > 0 && line[length - 1] == '\r')
        {
            length--;
        }

        return ParseConfiguredCodePage(line[..length]);
    }

    private static bool IsAsciiWhitespace(byte value)
    {
        return value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or 0x0B or 0x0C;
    }
}
