namespace OpenConquer.Rendering.Text.Codec;

internal enum EncodedTextTokenKind
{
    Glyph = 1,
    NewLine = 2,
    DataIcon = 3,
}

/// <summary>
/// Represents one semantic unit consumed from an encoded text stream.
/// </summary>
internal readonly record struct EncodedTextToken
{
    private readonly ushort _value;

    private EncodedTextToken(EncodedTextTokenKind kind, ushort value)
    {
        Kind = kind;
        _value = value;
    }

    public EncodedTextTokenKind Kind
    {
        get;
    }

    /// <summary>
    /// Gets the encoded glyph key carried by a glyph token.
    /// </summary>
    public ushort GlyphKey
    {
        get
        {
            if (Kind != EncodedTextTokenKind.Glyph)
            {
                throw new InvalidOperationException("The text token does not represent a glyph.");
            }

            return _value;
        }
    }

    /// <summary>
    /// Gets the decimal data-icon index encoded by a data-icon token.
    /// </summary>
    public byte DataIconIndex
    {
        get
        {
            if (Kind != EncodedTextTokenKind.DataIcon)
            {
                throw new InvalidOperationException("The text token does not represent a data icon.");
            }

            return checked((byte)_value);
        }
    }

    public static EncodedTextToken NewLine { get; } = new(EncodedTextTokenKind.NewLine, 0);

    public static EncodedTextToken CreateSingleByteGlyph(byte value)
    {
        return new EncodedTextToken(EncodedTextTokenKind.Glyph, value);
    }

    public static EncodedTextToken CreateDoubleByteGlyph(byte leadByte, byte trailByte)
    {
        ushort glyphKey = (ushort)((leadByte << 8) | trailByte);

        return new EncodedTextToken(EncodedTextTokenKind.Glyph, glyphKey);
    }

    public static EncodedTextToken CreateDataIcon(byte index)
    {
        if (index > 99)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Data-icon indices must be between 0 and 99.");
        }

        return new EncodedTextToken(EncodedTextTokenKind.DataIcon, index);
    }
}
