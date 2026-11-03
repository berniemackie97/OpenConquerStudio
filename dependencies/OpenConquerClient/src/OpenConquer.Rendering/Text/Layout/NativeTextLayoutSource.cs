using OpenConquer.Rendering.Text.Atlas;

namespace OpenConquer.Rendering.Text.Layout;

/// <summary>
/// Identifies the font configuration and glyph atlas a text layout was built from.
/// </summary>
internal sealed class NativeTextLayoutSource
{
    public NativeTextLayoutSource(NativeTextFontRecord primaryFont, GlyphAtlas atlas)
    {
        ArgumentNullException.ThrowIfNull(primaryFont);
        ArgumentNullException.ThrowIfNull(atlas);

        PrimaryFont = primaryFont;
        Atlas = atlas;
    }

    public NativeTextFontRecord PrimaryFont
    {
        get;
    }

    public GlyphAtlas Atlas
    {
        get;
    }
}
