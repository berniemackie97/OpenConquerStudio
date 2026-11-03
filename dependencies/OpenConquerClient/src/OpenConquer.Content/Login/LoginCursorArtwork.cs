using OpenConquer.Content.Images;
using OpenConquer.Content.Media;

namespace OpenConquer.Content.Login;

/// <summary>The five 32x32 frames of the retail <c>Normal.ani</c> cursor, its hotspot and frame duration.</summary>
public static class LoginCursorArtwork
{
    private static readonly EmbeddedImage[] s_frames =
    [
        new("Login/Cursor/Normal0.rgba.br", 32, 32, "21B4788930EB5F60B1BC0B904D4724E04A828505D9A5021FC6867D28D60C47D7"),
        new("Login/Cursor/Normal1.rgba.br", 32, 32, "EB2B3DEFAB9C823C8727B61259F6C3004FD83ADB3B7813BE2B5770836BCEFC75"),
        new("Login/Cursor/Normal2.rgba.br", 32, 32, "4FDFD09BD5A4744789D255512A877C62AD4D35308E0CBC9D521FD71AE3C904DE"),
        new("Login/Cursor/Normal3.rgba.br", 32, 32, "8B7AF6A11E4B0765F227254810D389D7D3B26B838AD2CE15DF59FAABC38F6E05"),
        new("Login/Cursor/Normal4.rgba.br", 32, 32, "54C287EAA5FC69B4C9838BEBBD2215F06E95BE9EB045F470251F924E69C800BE"),
    ];

    public static TimeSpan FrameDuration => TimeSpan.FromSeconds(10d / 60d);

    public static int HotspotX => 0;

    public static int HotspotY => 0;

    public static IReadOnlyList<RgbaImage> Load() => Array.AsReadOnly(Array.ConvertAll(s_frames, EmbeddedMedia.Load));
}
