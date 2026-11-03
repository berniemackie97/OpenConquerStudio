using System.Runtime.InteropServices;

namespace OpenConquer.Rendering.Text.Native;

/// <summary>
/// Native FreeType 2.14.3 ABI types required by the Rendering text subsystem.
/// </summary>
internal static class FreeTypeConstants
{
    public const int LoadDefault = 0;

    /// <summary><c>FT_LOAD_TARGET_MONO</c>: full grid fitting, so advances are the font's hinted (hdmx) widths.</summary>
    public const int LoadTargetMono = 2 << 16;
}

[Flags]
internal enum FreeTypeStyleFlags
{
    None = 0,
    Italic = 1 << 0,
    Bold = 1 << 1,
}

internal enum FreeTypeRenderMode
{
    Normal = 0,
    Light = 1,
    Mono = 2,
    Lcd = 3,
    LcdVertical = 4,
    Sdf = 5,
}

internal enum FreeTypePixelMode : byte
{
    None = 0,
    Mono = 1,
    Gray = 2,
    Gray2 = 3,
    Gray4 = 4,
    Lcd = 5,
    LcdVertical = 6,
    Bgra = 7,
}

[StructLayout(LayoutKind.Sequential)]
internal struct FreeTypeGeneric
{
    public nint Data;
    public nint Finalizer;
}

[StructLayout(LayoutKind.Sequential)]
internal struct FreeTypeVector
{
    public CLong X;
    public CLong Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct FreeTypeBoundingBox
{
    public CLong MinimumX;
    public CLong MinimumY;
    public CLong MaximumX;
    public CLong MaximumY;
}

[StructLayout(LayoutKind.Sequential)]
internal struct FreeTypeGlyphMetrics
{
    public CLong Width;
    public CLong Height;
    public CLong HorizontalBearingX;
    public CLong HorizontalBearingY;
    public CLong HorizontalAdvance;
    public CLong VerticalBearingX;
    public CLong VerticalBearingY;
    public CLong VerticalAdvance;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct FreeTypeBitmap
{
    public uint Rows;
    public uint Width;
    public int Pitch;
    public byte* Buffer;
    public ushort GrayLevels;
    public FreeTypePixelMode PixelMode;
    public byte PaletteMode;
    public void* Palette;
}

/// <summary>
/// Public prefix of FreeType's FT_GlyphSlotRec through bitmap_top.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct FreeTypeGlyphSlotRecord
{
    public nint Library;
    public FreeTypeFaceRecord* Face;
    public FreeTypeGlyphSlotRecord* Next;
    public uint GlyphIndex;
    public FreeTypeGeneric Generic;

    public FreeTypeGlyphMetrics Metrics;
    public CLong LinearHorizontalAdvance;
    public CLong LinearVerticalAdvance;
    public FreeTypeVector Advance;

    public int Format;

    public FreeTypeBitmap Bitmap;
    public int BitmapLeft;
    public int BitmapTop;
}

/// <summary>
/// Public prefix of FreeType's FT_FaceRec through glyph.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct FreeTypeFaceRecord
{
    public CLong FaceCount;
    public CLong FaceIndex;

    public CLong FaceFlags;
    public CLong StyleFlags;

    public CLong GlyphCount;

    public byte* FamilyName;
    public byte* StyleName;

    public int FixedSizeCount;
    public void* AvailableSizes;

    public int CharacterMapCount;
    public void* CharacterMaps;

    public FreeTypeGeneric Generic;

    public FreeTypeBoundingBox BoundingBox;

    public ushort UnitsPerEm;
    public short Ascender;
    public short Descender;
    public short Height;

    public short MaximumAdvanceWidth;
    public short MaximumAdvanceHeight;

    public short UnderlinePosition;
    public short UnderlineThickness;

    public FreeTypeGlyphSlotRecord* Glyph;
}
