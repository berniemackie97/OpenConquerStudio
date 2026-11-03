using System.Buffers;
using System.Text;

namespace OpenConquer.Rendering.Text.Codec;

/// <summary>
/// Converts encoded glyph keys into Unicode scalar values for rasterization.
/// </summary>
internal sealed class EncodedGlyphDecoder(int effectiveCodePage)
{
    private readonly Encoding? _encoding = ResolveEncoding(effectiveCodePage);

    /// <summary>
    /// Tries to decode a single- or double-byte glyph key into a single Unicode scalar value.
    /// </summary>
    public bool TryDecode(ushort glyphKey, out Rune character)
    {
        if (_encoding is null)
        {
            character = default;
            return false;
        }

        Span<byte> encodedBytes = stackalloc byte[2];
        int byteCount;

        byte leadByte = (byte)(glyphKey >> 8);

        if (leadByte == 0)
        {
            encodedBytes[0] = (byte)glyphKey;
            byteCount = 1;
        }
        else
        {
            encodedBytes[0] = leadByte;
            encodedBytes[1] = (byte)glyphKey;
            byteCount = 2;
        }

        ReadOnlySpan<byte> bytes = encodedBytes[..byteCount];

        try
        {
            int characterCount = _encoding.GetCharCount(bytes);

            if (characterCount is < 1 or > 2)
            {
                character = default;
                return false;
            }

            Span<char> characters = stackalloc char[2];
            int charactersWritten = _encoding.GetChars(bytes, characters);

            if (charactersWritten != characterCount)
            {
                character = default;
                return false;
            }

            OperationStatus status = Rune.DecodeFromUtf16(characters[..charactersWritten], out character, out int charactersConsumed);

            if (status == OperationStatus.Done && charactersConsumed == charactersWritten)
            {
                return true;
            }
        }
        catch (DecoderFallbackException)
        {
            // Invalid encoded byte sequences are treated as undecodable glyph keys.
        }

        character = default;
        return false;
    }

    private static Encoding? ResolveEncoding(int effectiveCodePage)
    {
        if (effectiveCodePage <= 0)
        {
            return null;
        }

        try
        {
            Encoding? encoding = CodePagesEncodingProvider.Instance.GetEncoding(effectiveCodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

            if (encoding is not null)
            {
                return encoding;
            }

            return Encoding.GetEncoding(effectiveCodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
