namespace OpenConquer.Content.Magic;

public sealed class MagicTypeRecord
{
    public const int NativeWordCount = 17;

    private readonly uint[] _nativeWords;

    internal MagicTypeRecord(ReadOnlySpan<uint> nativeWords)
    {
        if (nativeWords.Length != NativeWordCount)
        {
            throw new ArgumentException($"A native magic record must contain exactly {NativeWordCount} DWORDs.", nameof(nativeWords));
        }

        _nativeWords = nativeWords.ToArray();
    }

    public uint Type => _nativeWords[16];
    public uint Level => _nativeWords[6];
    public uint RequiredExperience => _nativeWords[5];
    public uint RequiredCharacterLevel => _nativeWords[7];
    public uint RequiredSubprofessionPhase => _nativeWords[12];
    public uint EncodedProfessionRequirement => _nativeWords[13];
    public uint RequiredSubprofessionClass => EncodedProfessionRequirement % 1000;

    public uint GetNativeWord(int index)
    {
        if ((uint)index >= NativeWordCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Native record index must be between 0 and {NativeWordCount - 1}.");
        }

        return _nativeWords[index];
    }
}
