using System.Globalization;

namespace OpenConquer.Content.Configuration;

/// <summary>
/// Resolves the client UI font size configuration used by the text pipeline.
/// </summary>
public sealed class ClientFontSizeConfiguration
{
    public const string RelativePath = "ini/info.ini";
    public const int DefaultNormalFontHeightPixels = 14;

    private const int MaximumFileLength = 64 * 1024;
    private const string FontSizeSectionName = "FontSize";
    private const string SizeKeyName = "Size";

    private ClientFontSizeConfiguration(int normalFontHeightPixels)
    {
        NormalFontHeightPixels = normalFontHeightPixels;
    }

    /// <summary>
    /// Gets the normal UI text height in pixels.
    /// </summary>
    public int NormalFontHeightPixels
    {
        get;
    }

    public static ClientFontSizeConfiguration Load(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        if (!contentSource.TryOpenRead(RelativePath, ContentLookupMode.LooseOnly, out Stream? stream))
        {
            return new ClientFontSizeConfiguration(DefaultNormalFontHeightPixels);
        }

        using (stream)
        {
            IniDocument document = IniDocument.Load(stream, RelativePath, MaximumFileLength);

            if (!document.TryGetValue(FontSizeSectionName, SizeKeyName, out string? value) ||
                !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int normalFontHeightPixels))
            {
                return new ClientFontSizeConfiguration(DefaultNormalFontHeightPixels);
            }

            return new ClientFontSizeConfiguration(normalFontHeightPixels);
        }
    }
}
