namespace OpenConquer.Rendering.Text.Fonts;

/// <summary>How glyph outlines are fitted to the pixel grid, and where a line's baseline sits.</summary>
public enum GlyphHinting
{
    /// <summary>FreeType's default hinting; the baseline sits the nominal pixel height below the line top (C3 fonts).</summary>
    Default,

    /// <summary>
    /// Windows GDI device text, as Flash draws device fonts: full grid fitting with the font's hinted advance widths, and
    /// the baseline <c>ceil(ascender * ppem / unitsPerEm)</c> below the line top (Arial 16 px: 15, 14 px: 13).
    /// </summary>
    Gdi,
}
