using OpenConquer.Content.Images;

namespace OpenConquer.Content.Ani;

/// <summary>
/// Resolves and decodes a complete ANI section.
/// </summary>
public static class AniFrameSetLoader
{
    public static AniFrameSet Load(IClientContentSource contentSource, string aniContentPath, string sectionName, ContentLookupMode mode)
    {
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(aniContentPath);
        ArgumentException.ThrowIfNullOrEmpty(sectionName);

        AniIndexSection section = AniIndexFile.Load(contentSource, aniContentPath, mode).GetRequiredSection(sectionName);

        return Load(contentSource, section, mode);
    }

    public static AniFrameSet Load(IClientContentSource contentSource, AniIndexSection section, ContentLookupMode mode)
    {
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentNullException.ThrowIfNull(section);

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown content lookup mode.");
        }

        if (section.FrameCount == 0)
        {
            throw new InvalidDataException($"ANI section [{section.Name}] contains no frames.");
        }

        RgbaImage[] frames = new RgbaImage[section.FrameCount];

        for (int frameIndex = 0; frameIndex < frames.Length; frameIndex++)
        {
            frames[frameIndex] = AniFrameLoader.LoadFrame(contentSource, section, frameIndex, mode);
        }

        return new AniFrameSet(section.Name, frames);
    }
}
