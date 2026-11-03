namespace OpenConquer.Rendering.Text.Codec;

/// <summary>
/// Reads encoded text as glyph, newline, and data-icon tokens using the configured character encoding.
/// </summary>
internal ref struct EncodedTextReader
{
    private const byte NewLineByte = (byte)'\n';
    private const byte DataIconEscapeByte = (byte)'#';

    private readonly ReadOnlySpan<byte> _encodedText;
    private readonly int _effectiveCodePage;
    private readonly bool _recognizeDataIcons;
    private int _byteIndex;

    public EncodedTextReader(ReadOnlySpan<byte> encodedText, int effectiveCodePage, bool recognizeDataIcons)
    {
        int nullIndex = encodedText.IndexOf((byte)0);

        _encodedText = nullIndex >= 0 ? encodedText[..nullIndex] : encodedText;

        _effectiveCodePage = effectiveCodePage;
        _recognizeDataIcons = recognizeDataIcons;
        _byteIndex = 0;
    }

    public bool TryRead(out EncodedTextToken token)
    {
        if (_byteIndex >= _encodedText.Length)
        {
            token = default;
            return false;
        }

        if (_recognizeDataIcons && TryReadDataIcon(out token))
        {
            return true;
        }

        byte currentByte = _encodedText[_byteIndex];

        if (currentByte == NewLineByte)
        {
            _byteIndex++;
            token = EncodedTextToken.NewLine;
            return true;
        }

        if (DbcsLeadByteClassifier.IsLeadByte(_effectiveCodePage, currentByte) && _byteIndex + 1 < _encodedText.Length)
        {
            byte trailByte = _encodedText[_byteIndex + 1];

            _byteIndex += 2;
            token = EncodedTextToken.CreateDoubleByteGlyph(currentByte, trailByte);
            return true;
        }

        _byteIndex++;
        token = EncodedTextToken.CreateSingleByteGlyph(currentByte);
        return true;
    }

    private bool TryReadDataIcon(out EncodedTextToken token)
    {
        if (_encodedText[_byteIndex] != DataIconEscapeByte || _byteIndex + 2 >= _encodedText.Length)
        {
            token = default;
            return false;
        }

        byte firstDigit = _encodedText[_byteIndex + 1];
        byte secondDigit = _encodedText[_byteIndex + 2];

        if (!IsAsciiDigit(firstDigit) || !IsAsciiDigit(secondDigit))
        {
            token = default;
            return false;
        }

        byte dataIconIndex = checked((byte)(((firstDigit - (byte)'0') * 10) + (secondDigit - (byte)'0')));

        _byteIndex += 3;
        token = EncodedTextToken.CreateDataIcon(dataIconIndex);
        return true;
    }

    private static bool IsAsciiDigit(byte value)
    {
        return value is >= (byte)'0' and <= (byte)'9';
    }
}
