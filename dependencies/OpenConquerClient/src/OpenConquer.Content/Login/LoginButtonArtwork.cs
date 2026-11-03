using OpenConquer.Content.Images;
using OpenConquer.Content.Media;

namespace OpenConquer.Content.Login;

public enum LoginButton
{
    /// <summary>start.dat DefineButton2 299, "Sign Up".</summary>
    Register,
    /// <summary>DefineButton2 305, "CP Purchase".</summary>
    Purchase,
    /// <summary>DefineButton2 310, "Help".</summary>
    Help,
    /// <summary>DefineButton2 335, the saved-account list arrow.</summary>
    Accounts,
    /// <summary>DefineButton2 342, save account.</summary>
    Save,
    /// <summary>DefineButton2 349, clear account.</summary>
    Clear,
    /// <summary>enterMC symbol 317.</summary>
    Enter,
    /// <summary>enterMC symbol 324.</summary>
    Exit,
    /// <summary>Region list leftBtn, DefineButton2 199 turned a quarter turn.</summary>
    RegionPagePrevious,
    /// <summary>Region list rightBtn, DefineButton2 192 turned a quarter turn.</summary>
    RegionPageNext,
}

public enum LoginButtonState
{
    Up,
    Over,
    Down,
}

/// <summary>The Up, Over and Down images of every login button.</summary>
public static class LoginButtonArtwork
{
    private const int StateCount = 3;

    // Ordered by button, then state.
    private static readonly EmbeddedImage[] s_images =
    [
        new("Login/Buttons/RegisterUp.rgba.br", 82, 21, "7D0C934BE3980D1D07D73706BBD4039F08BEF72892791F1BC9371952861E72C2"),
        new("Login/Buttons/RegisterOver.rgba.br", 82, 21, "6DEDA0F087EC41D3BDC106FAAE8EA98254F71107904592098CA4056D6CD1CF72"),
        new("Login/Buttons/RegisterDown.rgba.br", 82, 21, "97FC0FCD8B36EFA47EB0E9EF5D63D0AAA52F0B4E02228C924FEB4D8959026F8A"),
        new("Login/Buttons/PurchaseUp.rgba.br", 109, 21, "3853893B9C990EAD32D942FD264061F1EF4EE836DDD04D7185F736A2719EF7E7"),
        new("Login/Buttons/PurchaseOver.rgba.br", 109, 21, "48BCF7842A79254C3A12F2BED8E6106BAFCDA9D02CF1D409C27CFD739AF899CE"),
        new("Login/Buttons/PurchaseDown.rgba.br", 109, 21, "AF2FC4DF4FC45FA0DB2A1095AA43C7B0CE22CCB757E0CD9D8E64CDBF39022EBF"),
        new("Login/Buttons/HelpUp.rgba.br", 57, 23, "DB68CB506617B052CED2F0B2BE5C1AA07C08AC76A25A4C3664DD0FE6E2EE0795"),
        new("Login/Buttons/HelpOver.rgba.br", 57, 23, "32FA32C901DD08B18B5F8CB1BCBD8F7A7BF0ACF566446AEADCA1A8484A4D8011"),
        new("Login/Buttons/HelpDown.rgba.br", 57, 23, "818DE64B3668D39F0A85F29FD9B7E5C35426A0651D92B77BBBC3AC4DFC104F32"),
        new("Login/Buttons/AccountsUp.rgba.br", 17, 15, "759D4152237793B1FBC15B22C4F9494E31F7F655B8B0CAA4A081D82D0C84EC9F"),
        new("Login/Buttons/AccountsOver.rgba.br", 17, 15, "E955B2B76F807FAB7489F8528FB08F1C50B61ED7F0102F337B8337D0DD565F33"),
        new("Login/Buttons/AccountsDown.rgba.br", 17, 15, "1B228B6C9B28352B363494FD605FBD1DE530C3F8F19DC07B4FA4657FBA2A63C5"),
        new("Login/Buttons/SaveUp.rgba.br", 22, 19, "C28CBDA5B479CC4920300A97F3D298910272A32CD0CD0FB6812D9BA8F410BA3C"),
        new("Login/Buttons/SaveOver.rgba.br", 22, 19, "FC22C051FE6BE55D7B108D270419D859D8E0015A52987ACF68491EA0E360B1BF"),
        new("Login/Buttons/SaveDown.rgba.br", 22, 19, "19C8A36140D9661CBC0D4076E39A4928CD90C2314869CF01F05BBED0CBA133AB"),
        new("Login/Buttons/ClearUp.rgba.br", 20, 21, "E52660C2AAD3072110B29FB5FF5EFC9610C54143DD928BE079B55E5F00BBE065"),
        new("Login/Buttons/ClearOver.rgba.br", 20, 21, "1F09ECC6BF5212C7AB9F96DD1B015EEB3E3F7B42360EB68C7146522D06C58DED"),
        new("Login/Buttons/ClearDown.rgba.br", 20, 21, "E0A05885490EFC93676455ED8691ED80179963E7C33389E3C2779CFF166733BD"),
        new("Login/Buttons/EnterUp.rgba.br", 132, 40, "41FDEA3A98E653CB2C991CE9B656D41AC97D3E828CE32E1F5362174A51D3E404"),
        new("Login/Buttons/EnterOver.rgba.br", 132, 40, "8CA3DFFE0F2F1850EA61872E137003E54EC8C6A16D68606157AD6CBD85946B83"),
        new("Login/Buttons/EnterDown.rgba.br", 132, 40, "5056F3F7098A2D935AF7FAF550CD3CFF1CE5A4957832935C5FC2CDCA5DB212A8"),
        new("Login/Buttons/ExitUp.rgba.br", 132, 40, "57121511C04031B7CDE18EFC93A23728ADC19BAE695280F4D6A9678DE6283730"),
        new("Login/Buttons/ExitOver.rgba.br", 132, 40, "FE32A09DB879DDA1B73E8C5012CA8FAB5AA45B9FA845CCF4A03508660D89D794"),
        new("Login/Buttons/ExitDown.rgba.br", 132, 40, "37442D367079C3F6B1AD3E6C9E8FB7EABC615F10B10491AD631E4B9518A3BD1A"),
        new("Login/Buttons/PagePreviousUp.rgba.br", 38, 34, "E1FB515595C7AFDA502AD080CC7A6ED28E82470E6350DBD27B1029C3FDF10F17"),
        new("Login/Buttons/PagePreviousOver.rgba.br", 38, 34, "3CE1FE245673353A875A081AB526043CFBCF6BCAD7EBF6F8E3FD1882C0140940"),
        new("Login/Buttons/PagePreviousDown.rgba.br", 38, 34, "C486B81BAD06DB650ED6CDAC815EFDA70B2B51759DAAE1583B34E4B18C041CA9"),
        new("Login/Buttons/PageNextUp.rgba.br", 40, 34, "0FE48D296C7641580B0F11BF1C4ACA8D1D0A0ED76F7844010B3CD063BD896E04"),
        new("Login/Buttons/PageNextOver.rgba.br", 40, 34, "6D0121EA5B9DFE5DB32211E48028835A4CACDF53842C28ED68E89268AD4492CE"),
        new("Login/Buttons/PageNextDown.rgba.br", 40, 34, "5AED41328E2EAFC2575553AC973EC695B4C737FD52E0BBD92BE3BE3B358863F5"),
    ];

    public static RgbaImage Load(LoginButton button, LoginButtonState state)
    {
        if (!Enum.IsDefined(button))
        {
            throw new ArgumentOutOfRangeException(nameof(button));
        }

        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        return EmbeddedMedia.Load(s_images[(int)button * StateCount + (int)state]);
    }
}
