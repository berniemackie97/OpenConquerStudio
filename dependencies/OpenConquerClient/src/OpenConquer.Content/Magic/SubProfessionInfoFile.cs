using System.Globalization;
using System.Text;
using OpenConquer.Content.Configuration;

namespace OpenConquer.Content.Magic;

public sealed class SubProfessionInfoFile
{
    public const string RelativePath = "ini/SubProfessionInfo.ini";

    private const int MaximumFileLength = 1024 * 1024;

    private readonly IniDocument _document;

    private SubProfessionInfoFile(IniDocument document)
    {
        _document = document;
    }

    public static SubProfessionInfoFile Load(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        return new SubProfessionInfoFile(IniDocument.LoadRequired(contentSource, RelativePath, MaximumFileLength));
    }

    public static SubProfessionInfoFile Parse(ReadOnlySpan<byte> encodedFile)
    {
        if (encodedFile.Length > MaximumFileLength)
        {
            throw new InvalidDataException($"'{RelativePath}' exceeds the {MaximumFileLength}-byte safety limit.");
        }

        using MemoryStream stream = new(encodedFile.ToArray(), writable: false);
        return new SubProfessionInfoFile(IniDocument.Load(stream, RelativePath, MaximumFileLength));
    }

    public bool TryGetTitle(uint classId, out ReadOnlyMemory<byte> title)
    {
        if (classId == 0 || !_document.TryGetValue(classId.ToString(CultureInfo.InvariantCulture), "title", out string? value) || string.IsNullOrEmpty(value))
        {
            title = default;
            return false;
        }

        title = TranslateNativeEscapes(Encoding.Latin1.GetBytes(value));
        return true;
    }

    private static byte[] TranslateNativeEscapes(ReadOnlySpan<byte> encoded)
    {
        byte[] translated = new byte[encoded.Length];
        int written = 0;

        for (int index = 0; index < encoded.Length; index++)
        {
            if (encoded[index] == (byte)'\\' && index + 1 < encoded.Length)
            {
                byte next = encoded[index + 1];

                if (next is (byte)'n' or (byte)'t')
                {
                    translated[written++] = next == (byte)'n' ? (byte)'\n' : (byte)'\t';
                    index++;
                    continue;
                }
            }

            translated[written++] = encoded[index];
        }

        return translated.AsSpan(0, written).ToArray();
    }
}
