namespace OpenConquer.Content.Login;

public static class LoginLoadingContentPaths
{
    public const string Timeline = "ui/loading/timeline.json";

    public static IReadOnlyList<string> Paths
    {
        get;
    } =
    [
        "ui/loading/timeline.json",
        "ui/loading/artwork/images_374.tga",
        "ui/loading/artwork/images_376.tga",
        "ui/loading/artwork/images_381.tga",
        "ui/loading/artwork/images_382.tga",
        "ui/loading/artwork/shapes_261.tga",
        "ui/loading/artwork/shapes_375.tga",
        "ui/loading/artwork/shapes_377.tga",
        "ui/loading/artwork/shapes_379.tga",
        "ui/loading/artwork/shapes_383.tga",
        "ui/loading/artwork/shapes_389.tga",
        "ui/loading/artwork/shapes_390.tga",
        "ui/loading/artwork/shapes_391.tga",
        "ui/loading/artwork/shapes_392.tga",
        "ui/loading/artwork/sprite380.tga",
        "ui/loading/artwork/sprite387_f1.tga",
        "ui/loading/artwork/sprite387_f2.tga",
        "ui/loading/artwork/sprite387_f3.tga",
        "ui/loading/artwork/sprite387_f4.tga",
        "ui/loading/artwork/sprite387_f5.tga",
        "ui/loading/artwork/sprite387_f6.tga",
        "ui/loading/artwork/sprite387_f7.tga",
    ];

    public static bool IsPresent(IClientContentSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.TryOpenRead(Timeline, ContentLookupMode.LooseOnly, out Stream? stream))
        {
            return false;
        }

        stream!.Dispose();
        return true;
    }
}
