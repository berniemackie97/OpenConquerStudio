using System.Globalization;

namespace OpenConquer.Content.Configuration;

public sealed class StartupLogoConfiguration
{
    public const string RelativePath = "ini/info.ini";
    public const string DefaultBackgroundFormat = "Data/Main/Logo%d.bmp";

    private const int MaximumFileLength = 1024 * 1024;
    private const string SectionName = "DlgLogo";
    private const string BackgroundFormatKeyName = "BgFormat";

    private StartupLogoConfiguration(string backgroundFormat)
    {
        BackgroundFormat = backgroundFormat;
    }

    public string BackgroundFormat
    {
        get;
    }

    /// <summary>
    /// Reads <c>[DlgLogo] BgFormat</c>, falling back to the built in default.
    /// </summary>
    public static StartupLogoConfiguration LoadOrDefault(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        if (!contentSource.TryOpenRead(RelativePath, ContentLookupMode.LooseOnly, out Stream? stream))
        {
            return new StartupLogoConfiguration(DefaultBackgroundFormat);
        }

        using (stream)
        {
            IniDocument document = IniDocument.Load(stream, RelativePath, MaximumFileLength);

            if (!document.TryGetValue(SectionName, BackgroundFormatKeyName, out string? value) || string.IsNullOrEmpty(value))
            {
                return new StartupLogoConfiguration(DefaultBackgroundFormat);
            }

            return new StartupLogoConfiguration(value);
        }
    }

    public string GetLogoPath(int variantIndex)
    {
        if (variantIndex is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(variantIndex), variantIndex, "Startup logo variants are 1 and 2.");
        }

        string format = BackgroundFormat;

        int tokenIndex = format.IndexOf('%');

        if (tokenIndex < 0)
        {
            return format;
        }

        int specifierIndex = tokenIndex + 1;

        bool zeroPadded = false;

        if (specifierIndex < format.Length && format[specifierIndex] == '0')
        {
            zeroPadded = true;
            specifierIndex++;
        }

        int width = 0;

        while (specifierIndex < format.Length && char.IsAsciiDigit(format[specifierIndex]))
        {
            width = checked(width * 10 + format[specifierIndex] - '0');

            if (width > 9)
            {
                throw InvalidFormat(format);
            }

            specifierIndex++;
        }

        if (specifierIndex >= format.Length || format[specifierIndex] is not ('d' or 'i' or 'u'))
        {
            throw InvalidFormat(format);
        }

        if (format.AsSpan(specifierIndex + 1).Contains('%'))
        {
            throw InvalidFormat(format);
        }

        string replacement = variantIndex.ToString(CultureInfo.InvariantCulture);

        if (width > replacement.Length)
        {
            replacement = replacement.PadLeft(width, zeroPadded ? '0' : ' ');
        }

        return string.Concat(format.AsSpan(0, tokenIndex), replacement, format.AsSpan(specifierIndex + 1));
    }

    private static InvalidDataException InvalidFormat(string format)
    {
        return new InvalidDataException($"'{RelativePath}' contains unsupported [{SectionName}] {BackgroundFormatKeyName} format '{format}'.");
    }
}
