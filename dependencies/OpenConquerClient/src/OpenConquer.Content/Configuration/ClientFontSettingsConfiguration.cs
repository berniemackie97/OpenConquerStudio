using System.Text;

namespace OpenConquer.Content.Configuration;

/// <summary>
/// Loads the native client font settings consumed by GUI text rendering.
/// </summary>
public sealed class ClientFontSettingsConfiguration
{
    public const string RelativePath = "ini/FontSetting.ini";
    public const int DefaultChatFontHeightPixels = 14;
    public const int NormalRenderTextStyle = 0;
    public const int ShadowOffsetRenderTextStyle = 1;
    public const bool DefaultAntialiasEnabled = false;
    public const uint DefaultChatFontShadowColorArgb = 0;
    public const uint NativeDefaultCornerColorArgb = 0;
    public const int NativeDefaultCornerOffsetXPixels = 1;
    public const int NativeDefaultCornerOffsetYPixels = 1;

    private const int MaximumFileLength = 64 * 1024;

    private ClientFontSettingsConfiguration(int chatFontHeightPixels, int defaultRenderTextStyle, bool antialiasEnabled, bool guiFontShadowEnabled, string chatFontFaceName, string guiFontFaceName, uint chatFontShadowColorArgb, uint defaultCornerColorArgb)
    {
        ArgumentNullException.ThrowIfNull(chatFontFaceName);
        ArgumentNullException.ThrowIfNull(guiFontFaceName);

        ChatFontHeightPixels = chatFontHeightPixels;
        DefaultRenderTextStyle = defaultRenderTextStyle;
        AntialiasEnabled = antialiasEnabled;
        GuiFontShadowEnabled = guiFontShadowEnabled;
        ChatFontFaceName = chatFontFaceName;
        GuiFontFaceName = guiFontFaceName;
        ChatFontShadowColorArgb = chatFontShadowColorArgb;
        DefaultCornerColorArgb = defaultCornerColorArgb;
    }

    public int ChatFontHeightPixels
    {
        get;
    }

    public int DefaultRenderTextStyle
    {
        get;
    }

    public bool AntialiasEnabled
    {
        get;
    }

    public bool GuiFontShadowEnabled
    {
        get;
    }

    public string ChatFontFaceName
    {
        get;
    }

    public string GuiFontFaceName
    {
        get;
    }

    public uint ChatFontShadowColorArgb
    {
        get;
    }

    public uint DefaultCornerColorArgb
    {
        get;
    }
    public static int DefaultCornerOffsetXPixels => NativeDefaultCornerOffsetXPixels;
    public static int DefaultCornerOffsetYPixels => NativeDefaultCornerOffsetYPixels;

    public static ClientFontSettingsConfiguration Load(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        GameFontConfiguration gameFont = GameFontConfiguration.Load(contentSource);

        if (!contentSource.TryOpenRead(RelativePath, ContentLookupMode.LooseOnly, out Stream? stream))
        {
            return CreateDefaults(gameFont.FaceName);
        }

        using (stream)
            return Parse(ContentReader.ReadBytes(stream, RelativePath, MaximumFileLength), gameFont.FaceName);
    }

    internal static ClientFontSettingsConfiguration Parse(ReadOnlySpan<byte> bytes, string defaultFaceName)
    {
        ArgumentNullException.ThrowIfNull(defaultFaceName);

        string text = Encoding.Latin1.GetString(bytes);
        int chatFontHeightPixels = DefaultChatFontHeightPixels;
        int defaultRenderTextStyle = NormalRenderTextStyle;
        bool antialiasEnabled = DefaultAntialiasEnabled;
        bool guiFontShadowEnabled = false;
        string chatFontFaceName = string.Empty;
        string guiFontFaceName = defaultFaceName;
        uint chatFontShadowColorArgb = DefaultChatFontShadowColorArgb;
        uint defaultCornerColorArgb = NativeDefaultCornerColorArgb;
        int lineStart = 0;

        while (lineStart < text.Length)
        {
            int relativeLineFeedIndex = text.AsSpan(lineStart).IndexOf('\n');
            int lineEnd = relativeLineFeedIndex >= 0 ? lineStart + relativeLineFeedIndex : text.Length;
            ReadOnlySpan<char> line = text.AsSpan(lineStart, lineEnd - lineStart);
            int nullIndex = line.IndexOf('\0');

            if (nullIndex >= 0)
            {
                line = line[..nullIndex];
            }

            if (!line.IsEmpty && line[^1] == '\r')
            {
                line = line[..^1];
            }

            ParseLine(line, ref chatFontHeightPixels, ref defaultRenderTextStyle, ref antialiasEnabled, ref guiFontShadowEnabled, ref chatFontFaceName, ref guiFontFaceName, ref chatFontShadowColorArgb, ref defaultCornerColorArgb);

            if (relativeLineFeedIndex < 0)
            {
                break;
            }

            lineStart = lineEnd + 1;
        }

        if (chatFontFaceName.Length == 0)
        {
            chatFontFaceName = guiFontFaceName;
        }

        return new ClientFontSettingsConfiguration(chatFontHeightPixels, defaultRenderTextStyle, antialiasEnabled, guiFontShadowEnabled, chatFontFaceName, guiFontFaceName, chatFontShadowColorArgb, defaultCornerColorArgb);
    }

    private static ClientFontSettingsConfiguration CreateDefaults(string faceName)
    {
        ArgumentNullException.ThrowIfNull(faceName);
        return new ClientFontSettingsConfiguration(DefaultChatFontHeightPixels, NormalRenderTextStyle, DefaultAntialiasEnabled, guiFontShadowEnabled: false, faceName, faceName, DefaultChatFontShadowColorArgb, NativeDefaultCornerColorArgb);
    }

    private static void ParseLine(ReadOnlySpan<char> line, ref int chatFontHeightPixels, ref int defaultRenderTextStyle, ref bool antialiasEnabled, ref bool guiFontShadowEnabled, ref string chatFontFaceName, ref string guiFontFaceName, ref uint chatFontShadowColorArgb, ref uint defaultCornerColorArgb)
    {
        if (line.IsEmpty || line[0] == ';')
        {
            return;
        }

        int delimiterIndex = line.IndexOf('=');
        if (delimiterIndex < 0)
        {
            return;
        }

        ReadOnlySpan<char> key = line[..delimiterIndex];
        ReadOnlySpan<char> value = line[(delimiterIndex + 1)..];

        if (key.Equals("GUIFontShadow", StringComparison.OrdinalIgnoreCase))
        {
            guiFontShadowEnabled = ParseAtoi(value) == 1;
            if (guiFontShadowEnabled)
            {
                defaultRenderTextStyle = ShadowOffsetRenderTextStyle;
            }

            return;
        }

        if (key.Equals("GUIFontShadowColor", StringComparison.OrdinalIgnoreCase))
        {
            defaultCornerColorArgb = ParseUnsignedLongHex(value);
            return;
        }

        if (key.Equals("ChatFont", StringComparison.OrdinalIgnoreCase))
        {
            chatFontFaceName = value.ToString();
            return;
        }

        if (key.Equals("ChatFontSize", StringComparison.OrdinalIgnoreCase))
        {
            int parsed = ParseAtoi(value);
            chatFontHeightPixels = parsed == 0 ? 16 : parsed;
            return;
        }

        if (key.Equals("ChatFontShadowColor", StringComparison.OrdinalIgnoreCase))
        {
            chatFontShadowColorArgb = ParseUnsignedLongHex(value);
            return;
        }

        if (key.Equals("Antialias", StringComparison.OrdinalIgnoreCase))
        {
            antialiasEnabled = ParseAtoi(value) == 1;
            return;
        }

        if (key.Equals("GUIFont", StringComparison.OrdinalIgnoreCase))
        {
            guiFontFaceName = value.ToString();
        }
    }

    private static int ParseAtoi(ReadOnlySpan<char> value)
    {
        int index = 0;
        while (index < value.Length && IsAsciiWhiteSpace(value[index]))
            index++;

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

    private static uint ParseUnsignedLongHex(ReadOnlySpan<char> value)
    {
        int index = 0;
        while (index < value.Length && IsAsciiWhiteSpace(value[index]))
            index++;

        bool negative = false;
        if (index < value.Length && value[index] is '+' or '-')
        {
            negative = value[index] == '-';
            index++;
        }

        if (index + 1 < value.Length && value[index] == '0' && value[index + 1] is 'x' or 'X')
        {
            index += 2;
        }

        uint result = 0;
        bool hasDigits = false;

        while (index < value.Length)
        {
            int digit = HexadecimalDigit(value[index]);
            if (digit < 0)
            {
                break;
            }

            hasDigits = true;
            if (result > (uint.MaxValue - (uint)digit) / 16)
            {
                return uint.MaxValue;
            }

            result = (result * 16) + (uint)digit;
            index++;
        }

        if (!hasDigits)
        {
            return 0;
        }

        return negative ? unchecked(0u - result) : result;
    }

    private static int HexadecimalDigit(char character)
    {
        return character switch
        {
            >= '0' and <= '9' => character - '0',
            >= 'a' and <= 'f' => character - 'a' + 10,
            >= 'A' and <= 'F' => character - 'A' + 10,
            _ => -1
        };
    }

    private static bool IsAsciiWhiteSpace(char character) => character is ' ' or '\t' or '\n' or '\v' or '\f' or '\r';
}
