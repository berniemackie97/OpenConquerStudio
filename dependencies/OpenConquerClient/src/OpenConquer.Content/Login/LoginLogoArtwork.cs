using OpenConquer.Content.Images;
using OpenConquer.Content.Media;

namespace OpenConquer.Content.Login;

/// <summary>
/// logo.swf: a 25-frame, 30 fps Sorenson video with 18 encoded pictures; a frame without its own picture holds
/// the previous one. The movie has no stop action and loops for the whole session.
/// </summary>
public static class LoginLogoArtwork
{
    public const int FrameCount = 25;
    public const int FramesPerSecond = 30;
    public const int Width = 248;
    public const int Height = 78;

    private static readonly byte[] s_pictureByFrame = [0, 1, 2, 3, 4, 4, 5, 5, 6, 6, 7, 8, 9, 10, 10, 11, 12, 12, 13, 14, 14, 15, 16, 16, 17];

    private static readonly EmbeddedImage[] s_pictures =
    [
        new("Login/Logo/Picture00.rgba.br", 248, 78, "2C2810F30A3AAB5612CCF25F1E49380B64B261C914478C44A8AB0CF16A890734"),
        new("Login/Logo/Picture01.rgba.br", 248, 78, "5CE8C0E981FD81647D1992E6A7F7822A4FFC9C4ED40D44B15C6742911FC725A6"),
        new("Login/Logo/Picture02.rgba.br", 248, 78, "609825FA03D007AD8D7ED5F1EB00947E0FBE26538A8D3F3E833A0448C3985519"),
        new("Login/Logo/Picture03.rgba.br", 248, 78, "28A0AC26522448701BB8A9BB030F868E00EF7F1EF5C8510686125E5EAA976CA2"),
        new("Login/Logo/Picture04.rgba.br", 248, 78, "A2D44ED78915537E0EB12618D0280EA107D48A6ABA13A2238A9541D264C61997"),
        new("Login/Logo/Picture05.rgba.br", 248, 78, "0195D3D343AF5CCB1C4658EE03DA749306EC38F5703D732D1B64AAB296282BAA"),
        new("Login/Logo/Picture06.rgba.br", 248, 78, "5097DEB155A6EF9BF7D968344732F019031D667AFA6EBF09E0909CA2A44D7174"),
        new("Login/Logo/Picture07.rgba.br", 248, 78, "CC5A312BF2F398A9FE9FE69C4DB9176D5825C83C4B2DFAC713D9A767FAA1912E"),
        new("Login/Logo/Picture08.rgba.br", 248, 78, "A69ECD87E98D241EFE856197C59040FDDAD88E9DF745D39B21CEB54022B57F4F"),
        new("Login/Logo/Picture09.rgba.br", 248, 78, "4C90C9778101E50A6B5E09E69E997A5611E1CAE5D1636AF672C141104E9BD7F5"),
        new("Login/Logo/Picture10.rgba.br", 248, 78, "20E794478E0B5E6D6C13B1181CB46C20B26E05EDBF7BA88CD3079CF1001DF138"),
        new("Login/Logo/Picture11.rgba.br", 248, 78, "865634507BEC36AB2AC691F8F9924E50597814DBAA9298A19D1A49D925191D36"),
        new("Login/Logo/Picture12.rgba.br", 248, 78, "F0E18EBFD4441B222DE2007160A8C3235B96A1C864F33BAE89DF0CC9D1BB8A74"),
        new("Login/Logo/Picture13.rgba.br", 248, 78, "4334EBFA072E043B9A893B7CB2EFF53C6F1A2637E07AD7BE699B6A8C4C899714"),
        new("Login/Logo/Picture14.rgba.br", 248, 78, "449C5F770CE1ED11FE862C6A21E6CDAC87D470FCE6F43A7A95887A5461F5F1B4"),
        new("Login/Logo/Picture15.rgba.br", 248, 78, "58D64192BDD1BDCD58D802FFE20C8DA8B7955AB14C95EC5CD146CF9BED20D40A"),
        new("Login/Logo/Picture16.rgba.br", 248, 78, "04F716C1A8AC0BC8EAB7800943954DB3925D3DA3CB50A833BD2BD1C1620AC2DA"),
        new("Login/Logo/Picture17.rgba.br", 248, 78, "4A1BEDCE38970FBF3032BB2BECD3EAAC6CCDD165348BA5F6A691322FEA992282"),
    ];

    public static int PictureCount => s_pictures.Length;

    public static int PictureAt(int frame) =>
        (uint)frame < FrameCount ? s_pictureByFrame[frame] : throw new ArgumentOutOfRangeException(nameof(frame));

    public static RgbaImage LoadPicture(int picture) =>
        (uint)picture < (uint)s_pictures.Length ? EmbeddedMedia.Load(s_pictures[picture]) : throw new ArgumentOutOfRangeException(nameof(picture));
}
