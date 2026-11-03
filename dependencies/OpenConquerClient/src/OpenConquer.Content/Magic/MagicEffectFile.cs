using System.Globalization;
using System.Text;
using OpenConquer.Content.Configuration;

namespace OpenConquer.Content.Magic;

public enum MagicEffectTextKind : byte
{
    Description = 0,
    ExtendedDescription = 1,
    Name = 3,
}

public sealed class MagicEffectFile
{
    public const string RelativePath = "ini/MagicEffect.ini";

    private const int MaximumFileLength = 1024 * 1024;

    private readonly IniDocument? _document;

    private MagicEffectFile(IniDocument? document)
    {
        _document = document;
    }

    public static MagicEffectFile Load(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        if (!contentSource.TryOpenRead(RelativePath, ContentLookupMode.LooseOnly, out Stream? stream))
        {
            return new MagicEffectFile(null);
        }

        using (stream)
        {
            return new MagicEffectFile(IniDocument.Load(stream, RelativePath, MaximumFileLength));
        }
    }

    public static MagicEffectFile Parse(ReadOnlySpan<byte> encodedFile)
    {
        if (encodedFile.Length > MaximumFileLength)
        {
            throw new InvalidDataException($"'{RelativePath}' exceeds the {MaximumFileLength}-byte safety limit.");
        }

        using MemoryStream stream = new(encodedFile.ToArray(), writable: false);
        return new MagicEffectFile(IniDocument.Load(stream, RelativePath, MaximumFileLength));
    }

    public ReadOnlyMemory<byte> GetEncoded(uint type, uint level, MagicEffectTextKind kind)
    {
        string keyName = kind switch
        {
            MagicEffectTextKind.Description => "Desc",
            MagicEffectTextKind.ExtendedDescription => "DescEx",
            MagicEffectTextKind.Name => "Name",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown native magic-effect text kind."),
        };

        if (_document is null)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        string baseSection = GetSectionName(type, 0);
        _document.TryGetValue(baseSection, keyName, out string? value);

        if (level != 0)
        {
            string levelSection = GetSectionName(type, level);

            if (_document.TryGetValue(levelSection, keyName, out string? overrideValue) && !string.IsNullOrEmpty(overrideValue))
            {
                value = overrideValue;
            }
        }

        if (string.IsNullOrEmpty(value))
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        byte[] encoded = Encoding.Latin1.GetBytes(value);

        for (int index = 0; index < encoded.Length; index++)
        {
            if (encoded[index] == (byte)'~')
            {
                encoded[index] = (byte)' ';
            }
        }

        return encoded;
    }

    private static string GetSectionName(uint type, uint level)
    {
        return unchecked((int)(type * 10 + level)).ToString(CultureInfo.InvariantCulture);
    }
}
