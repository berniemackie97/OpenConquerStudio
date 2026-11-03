using System.Text;

namespace OpenConquer.Content.Login;

/// <summary>
/// Text of the retail login version label (start.dat <c>verTxt</c>), built the way <c>setVer</c> builds it.
/// </summary>
public static class ClientVersionLabel
{
    public const string RelativePath = "version.dat";

    // setVer: "version" + ": " followed by LoadVars.toString() up to its first '=' or '%'.
    private const string Prefix = "version: ";
    // setVer's failure branch appends this to the empty field: "version number read error".
    private const string ReadFailure = "版本号读取错误";
    private const int MaximumLength = 4096;

    public static string Load(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        if (!contentSource.TryOpenRead(RelativePath, ContentLookupMode.LooseOnly, out Stream? stream))
        {
            return ReadFailure;
        }

        using (stream)
        {
            return Format(ContentReader.ReadBytes(stream, RelativePath, MaximumLength));
        }
    }

    /// <summary>
    /// LoadVars parses the file as one variable name and toString() re-escapes it, so the label keeps
    /// the name's leading letters and digits; escape() turns anything else into a '%' sequence.
    /// </summary>
    public static string Format(ReadOnlySpan<byte> versionFile)
    {
        StringBuilder label = new(Prefix);

        foreach (byte value in versionFile)
        {
            if (!char.IsAsciiLetterOrDigit((char)value))
            {
                break;
            }

            label.Append((char)value);
        }

        return label.ToString();
    }
}
