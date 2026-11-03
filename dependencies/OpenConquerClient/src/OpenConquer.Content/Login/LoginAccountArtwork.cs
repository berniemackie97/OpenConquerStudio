using OpenConquer.Content.Images;
using OpenConquer.Content.Media;

namespace OpenConquer.Content.Login;

public enum LoginAccountDialog
{
    /// <summary>start.dat sprite 283, <c>saveMC</c>.</summary>
    Save,
    /// <summary>start.dat sprite 285, <c>clearMC</c>.</summary>
    Clear,
}

public enum LoginDialogButton
{
    /// <summary>DefineButton2 272.</summary>
    Ok,
    /// <summary>DefineButton2 282.</summary>
    Cancel,
}

/// <summary>Saved-account list rows and the save/clear confirmation dialogs.</summary>
public static class LoginAccountArtwork
{
    /// <summary>The ten-frame show, hold and hide timeline shared by both dialogs.</summary>
    public const int DialogFrameCount = 10;

    /// <summary>The frame both dialogs stop on while waiting for Ok or Cancel.</summary>
    public const int DialogHeldFrame = 6;

    // maskMC (sprite 262, shape 261) alpha multiplier on frames 2-9; frames 1 and 10 show nothing.
    private static readonly byte[] s_maskAlphaByFrame = [0, 0, 51, 103, 154, 205, 154, 103, 51, 0];

    /// <summary>Stage rows covered by the dialogs' black maskMC backdrop.</summary>
    public static (int Y, int Height) MaskRows => (90, 570);

    /// <summary>The maskMC alpha multiplier (of 256) on a one-based dialog frame.</summary>
    public static int MaskAlpha(int frame) =>
        frame is >= 1 and <= DialogFrameCount ? s_maskAlphaByFrame[frame - 1] : throw new ArgumentOutOfRangeException(nameof(frame));

    /// <summary>
    /// Each dialog as it holds at frame 6, cropped to where it differs from its <c>maskMC</c> backdrop and including
    /// the backdrop beneath it. The Up states of both buttons are part of the picture.
    /// </summary>
    private static readonly (EmbeddedImage Image, int StageX, int StageY)[] s_dialogs =
    [
        (new("Login/Account/SaveDialog.rgba.br", 344, 192, "0DFEF0C273B01298F22E65774432EC59B5D1F80C740B123DA0A158A5B8164739"), 344, 259),
        (new("Login/Account/ClearDialog.rgba.br", 296, 192, "BE53AEE9CDC977C15BADB9E5C3D884A1E4366176A63FEB5180C3C2AE32EAD9F0"), 361, 259),
    ];

    private static readonly EmbeddedImage[] s_rows =
    [
        new("Login/Account/Row.rgba.br", 166, 25, "81DF786E557AA0C2B044452B14D05C2174FDE84010F5E21B4E41A38DC3F0AD2B"),
        new("Login/Account/RowHighlighted.rgba.br", 166, 25, "860417D610D934DA6A8E014CB684173B8641B2CB1260F093EA039C20FC9573CA"),
    ];

    // Ordered by button, then Over and Down.
    private static readonly EmbeddedImage[] s_dialogButtons =
    [
        new("Login/Account/OkOver.rgba.br", 96, 26, "828D65A4FC3A999A28D0C4A582589ECDA6B3B2689E39710900DC5EFC6C840DA1"),
        new("Login/Account/OkDown.rgba.br", 96, 26, "F087E1F4F6232E45C137EB112D64D5288910DCB058CE11FA44CFA2CD8E778CFA"),
        new("Login/Account/CancelOver.rgba.br", 96, 26, "10629CD4129577E9D8F81234684C1EE0E108149C91A825F26DA72A789CAEE397"),
        new("Login/Account/CancelDown.rgba.br", 96, 26, "84A597C866A4699F4892AE8E1031D9C4B5B3D1C53CEB5FBA4C0F758EBDDBEDD3"),
    ];

    public static RgbaImage LoadDialog(LoginAccountDialog dialog) => EmbeddedMedia.Load(s_dialogs[(int)Validate(dialog)].Image);

    /// <summary>Top-left corner of the cropped dialog on the 1024x768 stage.</summary>
    public static (int X, int Y) DialogPosition(LoginAccountDialog dialog)
    {
        (_, int x, int y) = s_dialogs[(int)Validate(dialog)];
        return (x, y);
    }

    public static RgbaImage LoadRow(bool highlighted) => EmbeddedMedia.Load(s_rows[highlighted ? 1 : 0]);

    public static RgbaImage LoadDialogButton(LoginDialogButton button, LoginButtonState state)
    {
        if (!Enum.IsDefined(button))
        {
            throw new ArgumentOutOfRangeException(nameof(button));
        }

        if (state is not (LoginButtonState.Over or LoginButtonState.Down))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "A dialog's Up states are part of the dialog picture.");
        }

        return EmbeddedMedia.Load(s_dialogButtons[(int)button * 2 + (state == LoginButtonState.Down ? 1 : 0)]);
    }

    private static LoginAccountDialog Validate(LoginAccountDialog dialog) =>
        Enum.IsDefined(dialog) ? dialog : throw new ArgumentOutOfRangeException(nameof(dialog));
}
