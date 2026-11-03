using System.Globalization;
using System.Text;

namespace OpenConquer.Content.Configuration;

/// <summary>
/// Loads the game font configuration consumed during graphics startup.
/// </summary>
public sealed class GameFontConfiguration
{
    public const string RelativePath = "ini/Font.ini";
    public const int DefaultNominalPixelHeight = 12;

    private const int MaximumFileLength = 0x100;

    private GameFontConfiguration(string faceName, int nominalPixelHeight)
    {
        FaceName = faceName;
        NominalPixelHeight = nominalPixelHeight;
    }

    /// <summary>
    /// Gets the configured font face token exactly as stored before the final ASCII-space delimiter.
    /// </summary>
    public string FaceName
    {
        get;
    }

    public int NominalPixelHeight
    {
        get;
    }

    public static GameFontConfiguration Load(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        byte[] bytes = ContentReader.ReadRequiredBytes(contentSource, RelativePath, ContentLookupMode.LooseOnly, MaximumFileLength);

        return Parse(bytes);
    }

    internal static GameFontConfiguration Parse(ReadOnlySpan<byte> bytes)
    {
        int textLength = bytes.IndexOf((byte)0);

        if (textLength < 0)
        {
            textLength = bytes.Length;
        }

        ReadOnlySpan<byte> text = bytes[..textLength];
        int delimiterIndex = text.LastIndexOf((byte)' ');

        if (delimiterIndex <= 0)
        {
            throw new InvalidDataException($"'{RelativePath}' does not contain a font face followed by a nominal pixel height.");
        }

        string faceName = Encoding.Latin1.GetString(text[..delimiterIndex]);
        string nominalPixelHeightText = Encoding.Latin1.GetString(text[(delimiterIndex + 1)..]);

        if (!int.TryParse(nominalPixelHeightText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int nominalPixelHeight) || nominalPixelHeight == 0)
        {
            nominalPixelHeight = DefaultNominalPixelHeight;
        }

        return new GameFontConfiguration(faceName, nominalPixelHeight);
    }
}
