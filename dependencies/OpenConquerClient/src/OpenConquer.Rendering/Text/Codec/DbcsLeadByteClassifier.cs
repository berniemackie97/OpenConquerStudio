namespace OpenConquer.Rendering.Text.Codec;

/// <summary>
/// Classifies lead bytes for supported DBCS code pages.
/// </summary>
internal static class DbcsLeadByteClassifier
{
    public static bool IsLeadByte(int codePage, byte value)
    {
        return codePage switch
        {
            932 => value is >= 0x81 and <= 0x9F or >= 0xE0 and <= 0xFC,
            936 or 949 or 950 => value is >= 0x81 and <= 0xFE,
            1361 => value is >= 0x84 and <= 0xD3 or >= 0xD8 and <= 0xDE or >= 0xE0 and <= 0xF9,
            _ => false,
        };
    }
}
