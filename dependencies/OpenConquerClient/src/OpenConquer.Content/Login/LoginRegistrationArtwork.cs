using OpenConquer.Content.Images;

namespace OpenConquer.Content.Login;

/// <summary>
/// Artwork of the OpenConquer Sign Up dialog, assembled from retail pictures; retail 5517 opened a website
/// instead. The frame is clearMC's held picture with its flat body lengthened, and each field is the password box
/// of the account panel.
/// </summary>
public static class LoginRegistrationArtwork
{
    /// <summary>Rows added to clearMC's body to make room for three fields and a message.</summary>
    public const int BodyExtension = 40;

    public const int DialogWidth = 296;
    public const int DialogHeight = 192 + BodyExtension;

    /// <summary>First row of the footer divider in the lengthened picture.</summary>
    public const int FooterTopRow = FooterTop + BodyExtension;

    public const int FieldWidth = 149;
    public const int FieldHeight = 27;

    // clearMC's body is one repeated row from row 29 to row 144; its footer divider starts at row 145.
    internal const int BodyTop = 29;
    internal const int FooterTop = 145;

    // Shape 328's password box, border included, starts at (210, 65) in the panel.
    private const int FieldX = 210;
    private const int FieldY = 65;

    /// <summary>
    /// Top-left corner of the dialog on the 1024x768 stage: clearMC's position, raised by half the extension so the
    /// dialog keeps clearMC's centre.
    /// </summary>
    public static (int X, int Y) DialogPosition
    {
        get
        {
            (int x, int y) = LoginAccountArtwork.DialogPosition(LoginAccountDialog.Clear);
            return (x, y - BodyExtension / 2);
        }
    }

    /// <summary>clearMC's held picture, mask included, with <see cref="BodyExtension"/> more body rows.</summary>
    public static RgbaImage LoadDialog()
    {
        RgbaImage clear = LoginAccountArtwork.LoadDialog(LoginAccountDialog.Clear);
        if (clear.Width != DialogWidth || clear.Height + BodyExtension != DialogHeight)
        {
            throw new InvalidDataException("clearMC's held picture is not 296x192.");
        }

        int stride = clear.Width * 4;
        ReadOnlySpan<byte> source = clear.Pixels.Span;
        byte[] pixels = new byte[stride * DialogHeight];

        source[..(FooterTop * stride)].CopyTo(pixels);
        ReadOnlySpan<byte> body = source.Slice(BodyTop * stride, stride);
        for (int row = 0; row < BodyExtension; row++)
        {
            body.CopyTo(pixels.AsSpan((FooterTop + row) * stride));
        }

        source[(FooterTop * stride)..].CopyTo(pixels.AsSpan((FooterTop + BodyExtension) * stride));
        return new RgbaImage(DialogWidth, DialogHeight, pixels);
    }

    /// <summary>The account panel's password box, cut out with its border.</summary>
    public static RgbaImage LoadField()
    {
        RgbaImage panel = LoginStageArtwork.Load(LoginStageArtwork.Layer.AccountPanel);
        int sourceStride = panel.Width * 4;
        int stride = FieldWidth * 4;
        ReadOnlySpan<byte> source = panel.Pixels.Span;
        byte[] pixels = new byte[stride * FieldHeight];
        for (int row = 0; row < FieldHeight; row++)
        {
            source.Slice((FieldY + row) * sourceStride + FieldX * 4, stride).CopyTo(pixels.AsSpan(row * stride));
        }

        return new RgbaImage(FieldWidth, FieldHeight, pixels);
    }
}
