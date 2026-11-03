using OpenConquer.Content.Images;
using OpenConquer.Content.Media;

namespace OpenConquer.Content.Login;

/// <summary>Complete held pictures of retail errorMC and quitWindow, including the active button state.</summary>
public static class LoginErrorArtwork
{
    private static readonly EmbeddedImage[] s_pictures =
    [
        new("Login/Error/Error.rgba.br", 296, 192, "95DB6F8321AA6E65ED8DF46FFB65E46BF684EEB8C2062B4CD0C80E3DE3E8142D"),
        new("Login/Error/Quit.rgba.br", 296, 192, "60C097EF6E447B625D4153A074757F4E1ECA9066659D497F75DC58C4DEA7EEF8"),
        new("Login/Error/ErrorOkOver.rgba.br", 296, 192, "00BB5A44F1C2C10167972537B87B0926D29E299763B189F94921959E2C39A4DC"),
        new("Login/Error/ErrorOkDown.rgba.br", 296, 192, "86880932E2EEC9938702722F05BD2FF8473D5EB747323DD736B1BDB858968EFD"),
        new("Login/Error/QuitOkOver.rgba.br", 296, 192, "DDA5B83A1A544051B0458454BD553A357D69DFD35F6304CB89B505C7F75E1467"),
        new("Login/Error/QuitOkDown.rgba.br", 296, 192, "01B547548F1BF8EF3F6B09DCC8DEDC859E3AF7BFCC9D3A7955A556D420E4A9A2"),
        new("Login/Error/QuitCancelOver.rgba.br", 296, 192, "BC1870A33224867103091354F45280C65B6D81559D6AC1228D06ECD0BFFA76F3"),
        new("Login/Error/QuitCancelDown.rgba.br", 296, 192, "E393A31EE8705C99CDC221FDD7681D42B67301C851711E6728200CC68ADAC433"),
    ];

    public static RgbaImage Load(bool quit) => EmbeddedMedia.Load(s_pictures[quit ? 1 : 0]);

    public static RgbaImage Load(bool quit, LoginDialogButton button, LoginButtonState state)
    {
        if (!Enum.IsDefined(button) || !Enum.IsDefined(state) || (!quit && button == LoginDialogButton.Cancel))
        {
            throw new ArgumentOutOfRangeException(nameof(button));
        }
        if (state == LoginButtonState.Up)
        {
            return Load(quit);
        }
        int offset = state == LoginButtonState.Over ? 0 : 1;
        int index = !quit ? 2 : button == LoginDialogButton.Ok ? 4 : 6;
        return EmbeddedMedia.Load(s_pictures[index + offset]);
    }

    // Idle instances translate by(-1,0); loading quit adds one stage pixel.
    public static (int X, int Y) Position => (360, 259);
}
