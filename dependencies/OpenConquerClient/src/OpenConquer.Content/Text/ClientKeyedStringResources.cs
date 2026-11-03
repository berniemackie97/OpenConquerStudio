using System.Text;
using OpenConquer.Content.Configuration;

namespace OpenConquer.Content.Text;

/// <summary>
/// Resolves native keyed client strings while preserving their encoded bytes.
/// </summary>
public sealed class ClientKeyedStringResources
{
    public const string RetailRelativePath = "ini/Cn_Res.ini";

    private const string ConfigurationRelativePath = "ini/info.ini";
    private const string LanguageSectionName = "Language";
    private const string StringFileKeyName = "StringFile";
    private const int MaximumFileLength = 1024 * 1024;
    private const int MaximumConfigurationLength = 64 * 1024;
    private const int MaximumConfiguredPathLength = 260;
    private const int NativeLineBufferLength = 0x400;

    private readonly Dictionary<string, byte[]> _values;

    private ClientKeyedStringResources(Dictionary<string, byte[]> values)
    {
        _values = values;
    }

    public int Count => _values.Count;

    public static ClientKeyedStringResources Load(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        string? contentPath = ResolveConfiguredContentPath(contentSource);

        if (contentPath is null || !contentSource.TryOpenRead(contentPath, ContentLookupMode.LooseOnly, out Stream? resourceStream))
        {
            return new ClientKeyedStringResources(new Dictionary<string, byte[]>(StringComparer.Ordinal));
        }

        using (resourceStream)
        {
            byte[] encoded = ContentReader.ReadBytes(resourceStream, contentPath, MaximumFileLength);
            return Parse(encoded);
        }
    }

    public static string? ResolveConfiguredContentPath(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        if (!contentSource.TryOpenRead(ConfigurationRelativePath, ContentLookupMode.LooseOnly, out Stream? configurationStream))
        {
            return null;
        }

        using (configurationStream)
        {
            IniDocument configuration = IniDocument.Load(configurationStream, ConfigurationRelativePath, MaximumConfigurationLength);

            if (!configuration.TryGetValue(LanguageSectionName, StringFileKeyName, out string? configuredPath) ||
                string.IsNullOrEmpty(configuredPath))
            {
                return null;
            }

            return ClientContentPath.NormalizeVirtualPath(configuredPath, nameof(configuredPath), MaximumConfiguredPathLength);
        }
    }

    public static ClientKeyedStringResources Parse(ReadOnlySpan<byte> encodedFile)
    {
        if (encodedFile.Length > MaximumFileLength)
        {
            throw new InvalidDataException($"'{RetailRelativePath}' exceeds the {MaximumFileLength}-byte safety limit.");
        }

        Dictionary<string, byte[]> values = new(StringComparer.Ordinal);
        int position = 0;

        while (position < encodedFile.Length)
        {
            int length = Math.Min(NativeLineBufferLength - 1, encodedFile.Length - position);
            ReadOnlySpan<byte> remaining = encodedFile.Slice(position, length);
            int lineFeed = remaining.IndexOf((byte)'\n');

            if (lineFeed >= 0)
            {
                length = lineFeed + 1;
            }

            ReadOnlySpan<byte> line = encodedFile.Slice(position, length);
            position += length;

            ParseLine(line, values);
        }

        return new ClientKeyedStringResources(values);
    }

    public bool TryGetEncoded(string key, out ReadOnlyMemory<byte> value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_values.TryGetValue(key, out byte[]? encoded))
        {
            value = encoded;
            return true;
        }

        value = default;
        return false;
    }

    public ReadOnlyMemory<byte> GetEncoded(string key)
    {
        return TryGetEncoded(key, out ReadOnlyMemory<byte> value) ? value : ReadOnlyMemory<byte>.Empty;
    }

    private static void ParseLine(ReadOnlySpan<byte> line, Dictionary<string, byte[]> values)
    {
        int terminator = line.IndexOf((byte)0);

        if (terminator >= 0)
        {
            line = line[..terminator];
        }

        int separator = line.IndexOf((byte)'=');

        if (separator <= 0)
        {
            return;
        }

        ReadOnlySpan<byte> key = line[..separator];

        while (!key.IsEmpty && key[^1] is (byte)' ' or (byte)'\t')
        {
            key = key[..^1];
        }

        if (key.IsEmpty || key[0] == (byte)';')
        {
            return;
        }

        ReadOnlySpan<byte> value = line[(separator + 1)..];

        for (int index = 0; index < 2 && !value.IsEmpty && value[^1] is (byte)'\r' or (byte)'\n'; index++)
        {
            value = value[..^1];
        }

        values[Encoding.Latin1.GetString(key)] = value.ToArray();
    }
}
