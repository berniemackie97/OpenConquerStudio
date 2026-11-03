namespace OpenConquer.Content.Ani;

/// <summary>
/// Represents an ANI index section and its ordered frame content paths.
/// </summary>
public sealed class AniIndexSection
{
    internal AniIndexSection(string name, string[] framePaths)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(framePaths);

        Name = name;
        FramePaths = Array.AsReadOnly(framePaths);
    }

    public string Name
    {
        get;
    }

    public IReadOnlyList<string> FramePaths
    {
        get;
    }

    public int FrameCount => FramePaths.Count;
}
