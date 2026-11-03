using System.Globalization;

namespace OpenConquer.Content.Configuration;

public sealed class TryWrapTipConfiguration
{
    public const string RelativePath = "ini/info.ini";

    private const int MaximumFileLength = 64 * 1024;
    private const string SectionName = "TryWrapTip";

    private TryWrapTipConfiguration(int offsetX, int offsetY)
    {
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    public int OffsetX
    {
        get;
    }

    public int OffsetY
    {
        get;
    }

    public static TryWrapTipConfiguration Load(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        if (!contentSource.TryOpenRead(RelativePath, ContentLookupMode.LooseOnly, out Stream? stream))
        {
            return new TryWrapTipConfiguration(0, 0);
        }

        using (stream)
        {
            return Parse(IniDocument.Load(stream, RelativePath, MaximumFileLength));
        }
    }

    internal static TryWrapTipConfiguration Parse(IniDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new TryWrapTipConfiguration(
            ReadOffset(document, "OffsetX"),
            ReadOffset(document, "OffsetY"));
    }

    private static int ReadOffset(IniDocument document, string key)
    {
        if (!document.TryGetValue(SectionName, key, out string? encoded) || string.IsNullOrEmpty(encoded))
        {
            return 0;
        }

        ReadOnlySpan<char> value = encoded.AsSpan().TrimStart();
        int length = 0;

        if (!value.IsEmpty && value[0] is '+' or '-')
        {
            length++;
        }

        while (length < value.Length && value[length] is >= '0' and <= '9')
        {
            length++;
        }

        return int.TryParse(value[..length], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int offset)
            ? offset
            : 0;
    }
}
