using OpenConquer.Rendering.Text.Atlas;
using OpenConquer.Rendering.Text.Layout;

namespace OpenConquer.Rendering.Text.Rendering;

/// <summary>
/// Writes text glyph geometry in logical pixel coordinates.
/// </summary>
internal static class NativeTextGeometryBuilder
{
    public const int VerticesPerGlyphPass = 6;

    public static void WriteGlyph(Span<NativeTextVertex> destination, NativeTextLayoutItem item, NativeTextRenderPass pass, int originXPixels, int originYPixels)
    {
        if (destination.Length < VerticesPerGlyphPass)
        {
            throw new ArgumentException($"A glyph render pass requires space for {VerticesPerGlyphPass} vertices.", nameof(destination));
        }

        if (item.Kind != NativeTextLayoutItemKind.Glyph)
        {
            throw new ArgumentException("Text glyph geometry can only be written for glyph layout items.", nameof(item));
        }

        GlyphAtlasRegion region = item.AtlasRegion;

        long leftPixels = (long)originXPixels + item.XPixels + pass.OffsetXPixels;
        long topPixels = (long)originYPixels + item.YPixels + pass.OffsetYPixels;
        long rightPixels = leftPixels + region.WidthPixels;
        long bottomPixels = topPixels + region.HeightPixels;

        float left = leftPixels;
        float top = topPixels;
        float right = rightPixels;
        float bottom = bottomPixels;

        float textureLeft = ToAtlasCoordinate(region.XPixels);
        float textureTop = ToAtlasCoordinate(region.YPixels);
        float textureRight = ToAtlasCoordinate(region.XPixels + region.WidthPixels);
        float textureBottom = ToAtlasCoordinate(region.YPixels + region.HeightPixels);

        destination[0] = new NativeTextVertex(left, top, textureLeft, textureTop, pass.Colors.TopLeft);
        destination[1] = new NativeTextVertex(left, bottom, textureLeft, textureBottom, pass.Colors.BottomLeft);
        destination[2] = new NativeTextVertex(right, top, textureRight, textureTop, pass.Colors.TopRight);
        destination[3] = new NativeTextVertex(right, bottom, textureRight, textureBottom, pass.Colors.BottomRight);
        destination[4] = new NativeTextVertex(right, top, textureRight, textureTop, pass.Colors.TopRight);
        destination[5] = new NativeTextVertex(left, bottom, textureLeft, textureBottom, pass.Colors.BottomLeft);
    }

    private static float ToAtlasCoordinate(int pixel)
    {
        return pixel / (float)GlyphAtlasPage.SizePixels;
    }
}
