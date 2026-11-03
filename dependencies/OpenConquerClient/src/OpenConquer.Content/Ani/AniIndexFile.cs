using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace OpenConquer.Content.Ani;

/// <summary>
/// Parses and resolves sections from an ANI text index.
/// </summary>
public sealed class AniIndexFile
{
    private const int MaximumEncodedLength = 16 * 1024 * 1024;
    private const int MaximumFrameCount = 64;

    private readonly Dictionary<uint, AniIndexSection> _sectionsByHash;
    private readonly IReadOnlyList<AniIndexSection> _sections;

    private AniIndexFile(Dictionary<uint, AniIndexSection> sectionsByHash)
    {
        _sectionsByHash = sectionsByHash;
        _sections = Array.AsReadOnly(sectionsByHash.Values.OrderBy(static section => section.Name, StringComparer.Ordinal).ToArray());
    }

    public IReadOnlyList<AniIndexSection> Sections => _sections;

    public static AniIndexFile Load(IClientContentSource contentSource, string contentPath, ContentLookupMode mode)
    {
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentPath);

        byte[] payload = ContentReader.ReadRequiredBytes(contentSource, contentPath, mode, MaximumEncodedLength);
        return Parse(payload, contentPath);
    }

    public static AniIndexFile Load(Stream stream, string contentPath)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentPath);

        byte[] payload = ContentReader.ReadBytes(stream, contentPath, MaximumEncodedLength);
        return Parse(payload, contentPath);
    }

    public bool TryGetSection(string name, [NotNullWhen(true)] out AniIndexSection? section)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return _sectionsByHash.TryGetValue(ComputeSectionHash(name), out section);
    }

    public AniIndexSection GetRequiredSection(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (!TryGetSection(name, out AniIndexSection? section))
        {
            throw new InvalidDataException($"ANI index does not define section [{name}].");
        }

        return section;
    }

    private static AniIndexFile Parse(ReadOnlySpan<byte> payload, string contentPath)
    {
        string text = Encoding.Latin1.GetString(payload);
        Dictionary<uint, AniIndexSection> sectionsByHash = [];

        using StringReader reader = new(text);

        int lineNumber = 0;

        while (ReadLine(reader, ref lineNumber) is { } line)
        {
            if (!TryParseSectionHeader(line, out string? sectionName))
            {
                continue;
            }

            string? frameAmountLine = ReadLine(reader, ref lineNumber);

            if (frameAmountLine is null || !TryParseFrameAmount(frameAmountLine, out int frameCount))
            {
                throw InvalidLine(contentPath, lineNumber, $"section [{sectionName}] does not contain a valid FrameAmount");
            }

            if (frameCount < 0)
            {
                throw InvalidLine(contentPath, lineNumber, $"section [{sectionName}] contains a negative normalized frame count");
            }

            string[] framePaths = new string[frameCount];

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                string? frameLine = ReadLine(reader, ref lineNumber);

                if (frameLine is null || !TryParseFramePath(frameLine, frameIndex, out string? framePath))
                {
                    throw InvalidLine(contentPath, lineNumber, $"section [{sectionName}] does not contain a valid Frame{frameIndex}");
                }

                framePaths[frameIndex] = framePath;
            }

            sectionsByHash[ComputeSectionHash(sectionName)] = new AniIndexSection(sectionName, framePaths);
        }

        return new AniIndexFile(sectionsByHash);
    }

    private static uint ComputeSectionHash(string name)
    {
        uint hash = 0;

        foreach (char character in name)
        {
            if (character > byte.MaxValue)
            {
                throw new ArgumentException("ANI section names must contain only Latin-1 characters.", nameof(name));
            }

            hash = unchecked(hash * 33 + (uint)(int)(sbyte)(byte)character);
        }

        return hash;
    }

    private static string? ReadLine(StringReader reader, ref int lineNumber)
    {
        string? line = reader.ReadLine();

        if (line is not null)
        {
            lineNumber++;
        }

        return line;
    }

    private static bool TryParseSectionHeader(string line, [NotNullWhen(true)] out string? sectionName)
    {
        sectionName = null;

        if (line.Length < 3 || line[0] != '[')
        {
            return false;
        }

        int closingBracketIndex = line.IndexOf(']');

        if (closingBracketIndex <= 1)
        {
            return false;
        }

        sectionName = line[1..closingBracketIndex];
        return true;
    }

    private static bool TryParseFrameAmount(string line, out int frameCount)
    {
        const string Prefix = "FrameAmount=";

        frameCount = 0;

        if (!line.StartsWith(Prefix, StringComparison.Ordinal) || !int.TryParse(line.AsSpan(Prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rawFrameCount))
        {
            return false;
        }

        frameCount = NormalizeFrameCount(rawFrameCount);
        return frameCount <= MaximumFrameCount;
    }

    private static bool TryParseFramePath(string line, int frameIndex, [NotNullWhen(true)] out string? framePath)
    {
        string prefix = $"Frame{frameIndex.ToString(CultureInfo.InvariantCulture)}=";

        framePath = null;

        if (!line.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        ReadOnlySpan<char> value = line.AsSpan(prefix.Length);
        int whitespaceIndex = value.IndexOfAny(' ', '\t');

        if (whitespaceIndex >= 0)
        {
            value = value[..whitespaceIndex];
        }

        if (value.IsEmpty)
        {
            return false;
        }

        framePath = value.ToString();
        return true;
    }

    private static int NormalizeFrameCount(int rawFrameCount)
    {
        int normalized = rawFrameCount & unchecked((int)0x8000003F);

        if (normalized < 0)
        {
            normalized = ((normalized - 1) | unchecked((int)0xFFFFFFC0)) + 1;
        }

        return normalized;
    }

    private static InvalidDataException InvalidLine(string contentPath, int lineNumber, string message)
    {
        return new InvalidDataException($"ANI index '{contentPath}' line {lineNumber} is invalid: {message}.");
    }
}
