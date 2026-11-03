using System.Text;

namespace OpenConquer.Content.Configuration;

/// <summary>
/// Parses the INI grammar used by client configuration files.
/// </summary>
internal sealed class IniDocument
{
    private readonly Dictionary<string, Dictionary<string, string>> _sections;

    private IniDocument(Dictionary<string, Dictionary<string, string>> sections)
    {
        _sections = sections;
    }

    /// <summary>
    /// Loads a required INI document with <see cref="ContentLookupMode.LooseOnly"/>.
    /// </summary>
    public static IniDocument LoadRequired(IClientContentSource source, string contentPath, int maximumLength)
    {
        byte[] bytes = ContentReader.ReadRequiredBytes(source, contentPath, ContentLookupMode.LooseOnly, maximumLength);

        return Parse(bytes);
    }

    public static IniDocument Load(Stream stream, string contentPath, int maximumLength)
    {
        byte[] bytes = ContentReader.ReadBytes(stream, contentPath, maximumLength);

        return Parse(bytes);
    }

    public bool TryGetValue(string sectionName, string keyName, out string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyName);

        if (_sections.TryGetValue(sectionName, out Dictionary<string, string>? section) && section.TryGetValue(keyName, out string? foundValue))
        {
            value = foundValue;
            return true;
        }

        value = null;
        return false;
    }

    private static IniDocument Parse(ReadOnlySpan<byte> bytes)
    {
        string text = Encoding.Latin1.GetString(bytes);

        Dictionary<string, Dictionary<string, string>> sections = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? currentSection = null;

        int lineStart = 0;

        while (lineStart < text.Length)
        {
            int lineFeedIndex = text.IndexOf('\n', lineStart);

            bool hasLineFeed = lineFeedIndex >= 0;

            int lineEnd = hasLineFeed ? lineFeedIndex : text.Length;

            if (hasLineFeed && lineEnd > lineStart && text[lineEnd - 1] == '\r')
            {
                lineEnd--;
            }

            ReadOnlySpan<char> line = text.AsSpan(lineStart, lineEnd - lineStart);

            int nativeLineLength = line.Length + (hasLineFeed ? 1 : 0);

            if (TryParseSectionName(line, out string sectionName))
            {
                if (!sections.TryGetValue(sectionName, out currentSection))
                {
                    currentSection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    sections.Add(sectionName, currentSection);
                }
            }
            else if (currentSection is not null && TryParseKeyValue(line, nativeLineLength, out string keyName, out string value))
            {
                currentSection[keyName] = value;
            }

            if (!hasLineFeed)
            {
                break;
            }

            lineStart = lineFeedIndex + 1;
        }

        return new IniDocument(sections);
    }

    private static bool TryParseSectionName(ReadOnlySpan<char> line, out string sectionName)
    {
        if (line.Length == 0 || line[0] != '[')
        {
            sectionName = string.Empty;
            return false;
        }

        int relativeClosingBracketIndex = line[1..].IndexOf(']');

        if (relativeClosingBracketIndex < 0)
        {
            sectionName = string.Empty;
            return false;
        }

        sectionName = line.Slice(start: 1, length: relativeClosingBracketIndex).ToString();

        return true;
    }

    private static bool TryParseKeyValue(ReadOnlySpan<char> line, int nativeLineLength, out string keyName, out string value)
    {
        if (!IsCandidateKeyValueLine(line, nativeLineLength))
        {
            keyName = string.Empty;
            value = string.Empty;
            return false;
        }

        int delimiterIndex = line.IndexOf('=');

        if (delimiterIndex <= 0)
        {
            keyName = string.Empty;
            value = string.Empty;
            return false;
        }

        int keyEnd = delimiterIndex;

        while (keyEnd > 0 && line[keyEnd - 1] is ' ' or '\t')
        {
            keyEnd--;
        }

        if (keyEnd == 0)
        {
            keyName = string.Empty;
            value = string.Empty;
            return false;
        }

        keyName = line[..keyEnd].ToString();

        int valueStart = delimiterIndex + 1;

        while (valueStart < line.Length && line[valueStart] is ' ' or '\t')
        {
            valueStart++;
        }

        int valueEnd = valueStart;

        while (valueEnd < line.Length && line[valueEnd] is not ('\t' or ';' or '\r' or '\n'))
        {
            valueEnd++;
        }

        value = line[valueStart..valueEnd].ToString();

        return true;
    }

    private static bool IsCandidateKeyValueLine(ReadOnlySpan<char> line, int nativeLineLength)
    {
        return nativeLineLength > 2 && line.Length > 0 && line[0] is not ('\t' or '\n' or '\r' or ' ' or '/' or ';' or '=' or '\\');
    }
}
