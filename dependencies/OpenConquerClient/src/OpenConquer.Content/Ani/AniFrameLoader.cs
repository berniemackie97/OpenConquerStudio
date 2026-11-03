using OpenConquer.Content.Images;

namespace OpenConquer.Content.Ani;

/// <summary>
/// Resolves and decodes image frames referenced by ANI indexes.
/// </summary>
public static class AniFrameLoader
{
    public static RgbaImage Load(IClientContentSource contentSource, string aniContentPath, string sectionName, int frameIndex, ContentLookupMode mode)
    {
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(aniContentPath);
        ArgumentException.ThrowIfNullOrEmpty(sectionName);
        ArgumentOutOfRangeException.ThrowIfNegative(frameIndex);

        AniIndexSection section = AniIndexFile.Load(contentSource, aniContentPath, mode).GetRequiredSection(sectionName);

        return LoadFrame(contentSource, section, frameIndex, mode);
    }

    public static RgbaImage LoadWrappedFrame(IClientContentSource contentSource, string aniContentPath, string sectionName, uint frameIndex, ContentLookupMode mode)
    {
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(aniContentPath);
        ArgumentException.ThrowIfNullOrEmpty(sectionName);

        AniIndexSection section = AniIndexFile.Load(contentSource, aniContentPath, mode).GetRequiredSection(sectionName);

        return LoadWrappedFrame(contentSource, section, frameIndex, mode);
    }

    public static RgbaImage LoadWrappedFrame(IClientContentSource contentSource, AniIndexSection section, uint frameIndex, ContentLookupMode mode)
    {
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentNullException.ThrowIfNull(section);

        if (section.FrameCount == 0)
        {
            throw new InvalidDataException($"ANI section [{section.Name}] contains no frames.");
        }

        int normalizedFrameIndex = (int)(frameIndex % (uint)section.FrameCount);
        return LoadFrame(contentSource, section, normalizedFrameIndex, mode);
    }

    internal static RgbaImage LoadFrame(IClientContentSource contentSource, AniIndexSection section, int frameIndex, ContentLookupMode mode)
    {
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentNullException.ThrowIfNull(section);
        ArgumentOutOfRangeException.ThrowIfNegative(frameIndex);

        if (frameIndex >= section.FrameCount)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex), frameIndex, $"ANI section [{section.Name}] contains {section.FrameCount} frame(s).");
        }

        string frameContentPath = section.FramePaths[frameIndex];

        if (frameContentPath.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
        {
            return TargaImageLoader.Load(contentSource, frameContentPath, mode);
        }

        if (frameContentPath.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
        {
            return DdsImageLoader.Load(contentSource, frameContentPath, mode);
        }

        throw new InvalidDataException($"ANI section [{section.Name}] frame {frameIndex} references unsupported image '{frameContentPath}'.");
    }
}
