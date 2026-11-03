using OpenConquer.Content.Images;
using OpenConquer.Content.Media;

namespace OpenConquer.Content.Login;

/// <summary>Server tile art: the fallback tile movies and the start.dat sprite 179 rollover, selection and status sprites.</summary>
public static class LoginServerArtwork
{
    public enum Layer
    {
        /// <summary>unopenedS.swf, shown for an empty slot.</summary>
        Unopened,
        /// <summary>defaultS.swf, shown for a server without a picture.</summary>
        Default,
        /// <summary>Sprite 40, bounds (-17, -19.5), placed at (2.1, 1.75) and faded in on rollover.</summary>
        Highlight,
        /// <summary>Sprite 83, placed at (-22.65, -9) on rollover.</summary>
        Hover,
        /// <summary>Sprite 178, placed at (-26, -29) while selected.</summary>
        Selected,
        /// <summary>Sprite 34, the four status-light states.</summary>
        Status,
    }

    public const int HoverFrameCount = 34;
    public const int SelectedFrameCount = 47;
    /// <summary>Zero-based frame of sprite 178's "aa" label, where its loop restarts.</summary>
    public const int SelectedLoopStart = 22;
    public const int StatusStateCount = 4;

    private static readonly (EmbeddedImage Image, LoginArtworkSheet Sheet)[] s_layers =
    [
        (new("Login/Server/Unopened.rgba.br", 170, 90, "64511F3C4B1A4B072F7F09B243C88A22A70F19CB8441A586D4B512871DE7F9DE"), LoginArtworkSheet.OneFrame(170, 90)),
        (new("Login/Server/Default.rgba.br", 170, 90, "37A300257303589F9E1052D73805708807A9F1CCD466A8D927D4F491F3976C3B"), LoginArtworkSheet.OneFrame(170, 90)),
        (new("Login/Server/Highlight.rgba.br", 200, 100, "03184EB35B171363DDCE0438FC644458121449945DD7C898A05070CFA0248469"), LoginArtworkSheet.OneFrame(200, 100)),
        (new("Login/Server/Hover.rgba.br", 1600, 600, "C8695D2B639DA9B12B49EC0B1786AF58E62FD392C2B9FEA9489E9605519EE4A3"),
            new(1600, 600, 200, 120, Columns: 8, HoverFrameCount)),
        (new("Login/Server/Selected.rgba.br", 1760, 720, "C6DDCC3CE1ED6A121492763E369E534EBCBA5BD1A40EF902F5C2228A0C42938C"),
            new(1760, 720, 220, 120, Columns: 8, SelectedFrameCount)),
        (new("Login/Server/Status.rgba.br", 72, 18, "5FA3C29BE37231F2CB83C54440C737C45BEFFB10694687D40680224BE3A5C7E2"),
            new(72, 18, 18, 18, Columns: 4, StatusStateCount)),
    ];

    public static LoginArtworkSheet Describe(Layer layer) => s_layers[(int)Validate(layer)].Sheet;

    public static RgbaImage Load(Layer layer) => EmbeddedMedia.Load(s_layers[(int)Validate(layer)].Image);

    private static Layer Validate(Layer layer) => Enum.IsDefined(layer) ? layer : throw new ArgumentOutOfRangeException(nameof(layer));
}
