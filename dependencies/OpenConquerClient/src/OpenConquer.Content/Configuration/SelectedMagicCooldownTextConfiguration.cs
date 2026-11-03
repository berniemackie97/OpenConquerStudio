namespace OpenConquer.Content.Configuration;

/// <summary>
/// Loads the selected-magic cooldown-number configuration used by the main HUD.
/// </summary>
public sealed class SelectedMagicCooldownTextConfiguration
{
    public const string RelativePath = "ini/info.ini";
    public const int DefaultOffsetX = 0;
    public const int DefaultOffsetY = 0;
    public const int DefaultFontSizePixels = 20;
    public const uint DefaultColorArgb = uint.MaxValue;

    private const int MaximumFileLength = 64 * 1024;
    private const string SectionName = "SelectMagicNum";

    private SelectedMagicCooldownTextConfiguration(int offsetX, int offsetY, int fontSizePixels, uint colorArgb)
    {
        OffsetX = offsetX;
        OffsetY = offsetY;
        FontSizePixels = fontSizePixels;
        ColorArgb = colorArgb;
    }

    public int OffsetX
    {
        get;
    }

    public int OffsetY
    {
        get;
    }

    public int FontSizePixels
    {
        get;
    }

    public uint ColorArgb
    {
        get;
    }

    public static SelectedMagicCooldownTextConfiguration Load(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        IniDocument document = IniDocument.LoadRequired(contentSource, RelativePath, MaximumFileLength);

        return new SelectedMagicCooldownTextConfiguration(
            ReadInt32(document, "OffsetX", DefaultOffsetX),
            ReadInt32(document, "OffsetY", DefaultOffsetY),
            ReadInt32(document, "FontSize", DefaultFontSizePixels),
            unchecked((uint)ReadInt32(document, "Color", unchecked((int)DefaultColorArgb))));
    }

    private static int ReadInt32(IniDocument document, string keyName, int defaultValue)
    {
        if (!document.TryGetValue(SectionName, keyName, out string? value) || string.IsNullOrEmpty(value))
        {
            return defaultValue;
        }

        return ParseNativeInt32(value, defaultValue);
    }

    private static int ParseNativeInt32(ReadOnlySpan<char> value, int defaultValue)
    {
        if (value.Length > 2 && value[0] == '0' && value[1] is 'x' or 'X')
        {
            return ParseHexadecimal(value[2..], defaultValue);
        }

        return ParseDecimal(value);
    }

    private static int ParseDecimal(ReadOnlySpan<char> value)
    {
        int index = 0;

        while (index < value.Length && IsAsciiWhiteSpace(value[index]))
        {
            index++;
        }

        bool negative = false;

        if (index < value.Length)
        {
            if (value[index] == '+')
            {
                index++;
            }
            else if (value[index] == '-')
            {
                negative = true;
                index++;
            }
        }

        long limit = negative ? 2147483648L : int.MaxValue;
        long result = 0;

        while (index < value.Length && value[index] is >= '0' and <= '9')
        {
            int digit = value[index] - '0';

            if (result > (limit - digit) / 10)
            {
                return negative ? int.MinValue : int.MaxValue;
            }

            result = (result * 10) + digit;
            index++;
        }

        if (!negative)
        {
            return (int)result;
        }

        return result == 2147483648L ? int.MinValue : -(int)result;
    }

    private static int ParseHexadecimal(ReadOnlySpan<char> value, int defaultValue)
    {
        uint result = 0;
        bool hasDigits = false;

        foreach (char character in value)
        {
            int digit = HexadecimalDigit(character);

            if (digit < 0)
            {
                break;
            }

            hasDigits = true;

            if (result > (uint.MaxValue - (uint)digit) / 16)
            {
                return -1;
            }

            result = (result * 16) + (uint)digit;
        }

        return hasDigits ? unchecked((int)result) : defaultValue;
    }

    private static int HexadecimalDigit(char character)
    {
        if (character is >= '0' and <= '9')
        {
            return character - '0';
        }

        if (character is >= 'a' and <= 'f')
        {
            return character - 'a' + 10;
        }

        if (character is >= 'A' and <= 'F')
        {
            return character - 'A' + 10;
        }

        return -1;
    }

    private static bool IsAsciiWhiteSpace(char character) =>
        character is ' ' or '\t' or '\n' or '\v' or '\f' or '\r';
}
