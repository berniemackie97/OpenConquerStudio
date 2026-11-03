namespace OpenConquer.Rendering.Text.Fonts.Discovery;

/// <summary>
/// Identifies a font file registered with the host font system and, when known,
/// a specific face within that file.
/// </summary>
internal sealed class HostFontReference
{
    public HostFontReference(string filePath, int? faceIndex = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (faceIndex is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(faceIndex), faceIndex, "Font face index cannot be negative.");
        }

        FilePath = filePath;
        FaceIndex = faceIndex;
    }

    public string FilePath
    {
        get;
    }

    /// <summary>
    /// Gets the zero-based face index reported by the host font system.
    /// A null value means the host identified the font file but not a specific face.
    /// </summary>
    public int? FaceIndex
    {
        get;
    }
}
