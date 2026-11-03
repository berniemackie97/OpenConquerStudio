using System.Text;

namespace OpenConquer.Content.Role;

/// <summary>
/// <c>ini/selectCharacter.ini</c> as role.dat's <c>CIni</c> class reads it: class descriptions, confirm-panel
/// labels, logo position and sound file names.
/// </summary>
public sealed class SelectCharacterIni
{
    public const string RelativePath = "ini/selectCharacter.ini";

    private const int MaximumLength = 1 << 20;

    private readonly Dictionary<string, Dictionary<string, string>> _sections;

    private SelectCharacterIni(Dictionary<string, Dictionary<string, string>> sections) => _sections = sections;

    /// <summary>Reads the original role configuration from the client content source.</summary>
    public static SelectCharacterIni Load(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        using Stream stream = contentSource.OpenRequiredRead(RelativePath, ContentLookupMode.LooseOnly);
        return Parse(ContentReader.ReadBytes(stream, RelativePath, MaximumLength));
    }

    /// <summary>
    /// CIni loads the file with LoadVars, which URL-decodes it (<c>+</c> is a space, <c>%xx</c> a byte); without an
    /// <c>&amp;</c> the whole file is one LoadVars pair and survives the class's toString/unescape round trip.
    /// Each line then loses its last character (the CR), is trimmed of characters up to 0x20, and is cut at
    /// <c>//</c>. A <c>[name]</c> line opens a section; any other line is kept only if it has exactly one <c>=</c>
    /// and a name without spaces, and <c>~</c> in its value becomes a space. A repeated section starts over.
    /// </summary>
    public static SelectCharacterIni Parse(ReadOnlySpan<byte> file)
    {
        string text = UrlDecode(file);
        string[] lines = text.Split('\n');
        lines[^1] += " ";

        Dictionary<string, Dictionary<string, string>> sections = new(StringComparer.OrdinalIgnoreCase)
        {
            [string.Empty] = new(StringComparer.OrdinalIgnoreCase)
        };
        Dictionary<string, string> current = sections[string.Empty];

        foreach (string raw in lines)
        {
            string line = ClearEdge(raw.Length == 0 ? raw : raw[..^1]);
            if (line.StartsWith('['))
            {
                int end = line.IndexOf(']', StringComparison.Ordinal);
                string name = ClearEdge(end < 0 ? line[1..Math.Max(1, line.Length - 1)] : line[1..end]);
                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                sections[name] = current;
                continue;
            }

            string[] parts = line.Split('=');
            if (line.Length == 0 || parts.Length != 2)
            {
                continue;
            }

            string key = ClearEdge(parts[0]);
            if (key.Length == 0 || key.Contains(' ', StringComparison.Ordinal))
            {
                continue;
            }

            current[key] = ClearEdge(parts[1]).Replace('~', ' ');
        }

        return new SelectCharacterIni(sections);
    }

    /// <summary>The value of <paramref name="key"/> in <paramref name="section"/>, or null when absent.</summary>
    public string? Get(string section, string key)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(key);

        return _sections.TryGetValue(section, out Dictionary<string, string>? values) && values.TryGetValue(key, out string? value) ? value : null;
    }

    /// <summary>Every key of <paramref name="section"/> in file order, or none.</summary>
    public IReadOnlyDictionary<string, string> Section(string section)
    {
        ArgumentNullException.ThrowIfNull(section);

        return _sections.TryGetValue(section, out Dictionary<string, string>? values) ? values : new Dictionary<string, string>();
    }

    private static string ClearEdge(string value)
    {
        int start = 0;
        while (start < value.Length && value[start] <= ' ')
        {
            start++;
        }

        int end = value.Length;
        while (end > start && value[end - 1] <= ' ')
        {
            end--;
        }

        string trimmed = value[start..end];
        int comment = trimmed.IndexOf("//", StringComparison.Ordinal);
        return comment < 0 ? trimmed : trimmed[..comment];
    }

    private static string UrlDecode(ReadOnlySpan<byte> file)
    {
        // System.useCodepage reads the file in the system code page; the retail file is ASCII.
        List<byte> bytes = new(file.Length);
        for (int index = 0; index < file.Length; index++)
        {
            byte value = file[index];
            if (value == (byte)'+')
            {
                bytes.Add((byte)' ');
            }
            else if (value == (byte)'%' && index + 2 < file.Length && IsHex(file[index + 1]) && IsHex(file[index + 2]))
            {
                bytes.Add((byte)Convert.ToInt32(Encoding.ASCII.GetString(file.Slice(index + 1, 2)), 16));
                index += 2;
            }
            else
            {
                bytes.Add(value);
            }
        }

        return Encoding.Latin1.GetString([.. bytes]);
    }

    private static bool IsHex(byte value) => char.IsAsciiHexDigit((char)value);
}
