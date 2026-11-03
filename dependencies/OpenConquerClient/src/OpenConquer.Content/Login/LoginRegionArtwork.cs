using OpenConquer.Content.Images;
using OpenConquer.Content.Media;

namespace OpenConquer.Content.Login;

/// <summary>
/// Region tile art from <c>defaultR.swf</c> and <c>unopenedR.swf</c>, scaled by the region panel's matrix and
/// positioned relative to a tile's origin.
/// </summary>
public static class LoginRegionArtwork
{
    public enum Layer
    {
        /// <summary>defaultR shape 3 without its caption, at (0, -1).</summary>
        Tile,
        /// <summary>unopenedR sprite 3, at (0, 0).</summary>
        Unopened,
        /// <summary>defaultR sprite 9 for timeline frames 2-8 and 10-12, at (-10, -6).</summary>
        Hover,
        /// <summary>defaultR sprite 60's 25-frame loop inside sprite 61, at (2, 0).</summary>
        Flame,
    }

    public const int HoverFrameCount = 10;
    public const int FlameFrameCount = 25;

    private static readonly (EmbeddedImage Image, LoginArtworkSheet Sheet, int OffsetX, int OffsetY)[] s_layers =
    [
        (new("Login/Region/Tile.rgba.br", 151, 53, "466A1A4A2219102246874C9407FBC8790A4F7828C465692B8321091D241CBF38"),
            LoginArtworkSheet.OneFrame(151, 53), 0, -1),
        (new("Login/Region/Unopened.rgba.br", 151, 53, "BBCC706CC21EFD39DAF06B306C5A8D5A185037288F8787378FD7BD86DC81424F"),
            LoginArtworkSheet.OneFrame(151, 53), 0, 0),
        (new("Login/Region/Hover.rgba.br", 850, 130, "B3558A7B60937C264B15D6D23EBA65E924F2DACE7374ACD7F4A0C4EE2DC233E2"),
            new(850, 130, 170, 65, Columns: 5, HoverFrameCount), -10, -6),
        (new("Login/Region/Flame.rgba.br", 740, 250, "E8480C78CB0E8E51BA83B340034B7E84CE333A009092678C1111DDC9146EE5C2"),
            new(740, 250, 148, 50, Columns: 5, FlameFrameCount), 2, 0),
    ];

    public static LoginArtworkSheet Describe(Layer layer) => s_layers[(int)Validate(layer)].Sheet;

    /// <summary>The layer's top-left corner relative to the tile origin, in 1024x768 stage pixels.</summary>
    public static (int X, int Y) Offset(Layer layer)
    {
        (_, _, int x, int y) = s_layers[(int)Validate(layer)];
        return (x, y);
    }

    public static RgbaImage Load(Layer layer) => EmbeddedMedia.Load(s_layers[(int)Validate(layer)].Image);

    private static Layer Validate(Layer layer) => Enum.IsDefined(layer) ? layer : throw new ArgumentOutOfRangeException(nameof(layer));
}
