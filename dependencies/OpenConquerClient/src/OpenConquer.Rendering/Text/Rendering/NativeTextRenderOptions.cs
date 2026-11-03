using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Rendering.Text.Rendering;

/// <summary>
/// Caller settings for one text render.
/// </summary>
public readonly record struct NativeTextRenderOptions
{
    public NativeTextRenderOptions(NativeTextRenderStyle style, SpriteColor textColor, SpriteColor cornerColor, int cornerOffsetXPixels, int cornerOffsetYPixels, NativeTextVertexColors perCornerColors)
    {
        if (!Enum.IsDefined(style))
        {
            throw new ArgumentOutOfRangeException(nameof(style), style, "Unknown native text render style.");
        }

        Style = style;
        TextColor = textColor;
        CornerColor = cornerColor;
        CornerOffsetXPixels = cornerOffsetXPixels;
        CornerOffsetYPixels = cornerOffsetYPixels;
        PerCornerColors = perCornerColors;
    }

    public NativeTextRenderStyle Style
    {
        get;
    }

    public SpriteColor TextColor
    {
        get;
    }

    public SpriteColor CornerColor
    {
        get;
    }

    public int CornerOffsetXPixels
    {
        get;
    }

    public int CornerOffsetYPixels
    {
        get;
    }

    public NativeTextVertexColors PerCornerColors
    {
        get;
    }
}
