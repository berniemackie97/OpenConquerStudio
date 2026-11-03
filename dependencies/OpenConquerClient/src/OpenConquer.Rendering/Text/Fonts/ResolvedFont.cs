namespace OpenConquer.Rendering.Text.Fonts;

/// <summary>
/// Identifies one resolved font face within a concrete font file.
/// </summary>
internal sealed class ResolvedFont
{
    public ResolvedFont(string filePath, int faceIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentOutOfRangeException.ThrowIfNegative(faceIndex);

        FilePath = filePath;
        FaceIndex = faceIndex;
    }

    /// <summary>
    /// Gets the path to the font file containing the resolved face.
    /// </summary>
    public string FilePath
    {
        get;
    }

    /// <summary>
    /// Gets the zero-based face index within the font file.
    /// </summary>
    public int FaceIndex
    {
        get;
    }
}
