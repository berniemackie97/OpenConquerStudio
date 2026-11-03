using OpenConquer.Content.Images;
using OpenConquer.Content.Media;

namespace OpenConquer.Content.Login;

/// <summary>The fixed layers of the retail login stage, already placed on the 1024x768 stage grid.</summary>
public static class LoginStageArtwork
{
    public enum Layer
    {
        /// <summary>start.dat shape 11 (bitmap 10), drawn from stage y 41.</summary>
        Backdrop,
        /// <summary>start.dat shape 400, the full-stage top and bottom ornament.</summary>
        Border,
        /// <summary>start.dat shape 328, the account panel inside <c>enterMC</c>.</summary>
        AccountPanel,
        /// <summary>start.dat sprite 183 with its 195/256 alpha, the region list background.</summary>
        RegionPanel,
    }

    private static readonly EmbeddedImage[] s_layers =
    [
        new("Login/Stage/Backdrop.rgba.br", 1024, 637, "E33BF0450C7A386AA28D6D90A1F71A8B7D8B64E1E4EC92FDFAD48D7F3880A2B9"),
        new("Login/Stage/Border.rgba.br", 1024, 768, "76A47E69EF101456A8925BB84A137B3D738C8BF6BCF7AD2F0DED9A0FD5CC5AA8"),
        new("Login/Stage/AccountPanel.rgba.br", 508, 98, "3A1E4698639F2C4AF2EACE1C9B48E81D4898D279831C128BBE65E76E3EB240C2"),
        new("Login/Stage/RegionPanel.rgba.br", 340, 434, "DF314FC0B3E49BA4255E05750F72BF0F765C79DD0B71883E149AD1343342986B"),
    ];

    public static RgbaImage Load(Layer layer) => EmbeddedMedia.Load(s_layers[(int)Validate(layer)]);

    private static Layer Validate(Layer layer) => Enum.IsDefined(layer) ? layer : throw new ArgumentOutOfRangeException(nameof(layer));
}
