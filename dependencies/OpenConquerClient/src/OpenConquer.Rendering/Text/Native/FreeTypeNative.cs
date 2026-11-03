using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OpenConquer.Rendering.Text.Native;

/// <summary>
/// Narrow native interop surface for the stock FreeType ABI used by Rendering text.
/// </summary>
internal static unsafe partial class FreeTypeNative
{
    private const string LibraryName = "freetype";

    [LibraryImport(LibraryName, EntryPoint = "FT_Init_FreeType")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int InitFreeType(nint* library);

    [LibraryImport(LibraryName, EntryPoint = "FT_Done_FreeType")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int DoneFreeType(nint library);

    [LibraryImport(LibraryName, EntryPoint = "FT_Library_Version")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void LibraryVersion(nint library, int* major, int* minor, int* patch);

    [LibraryImport(LibraryName, EntryPoint = "FT_New_Memory_Face")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int NewMemoryFace(nint library, byte* fileBase, CLong fileSize, CLong faceIndex, FreeTypeFaceRecord** face);

    [LibraryImport(LibraryName, EntryPoint = "FT_Done_Face")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int DoneFace(FreeTypeFaceRecord* face);

    [LibraryImport(LibraryName, EntryPoint = "FT_Set_Char_Size")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int SetCharSize(FreeTypeFaceRecord* face, CLong characterWidth, CLong characterHeight, uint horizontalResolution, uint verticalResolution);

    [LibraryImport(LibraryName, EntryPoint = "FT_Get_Char_Index")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial uint GetCharIndex(FreeTypeFaceRecord* face, CULong characterCode);

    [LibraryImport(LibraryName, EntryPoint = "FT_Load_Glyph")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int LoadGlyph(FreeTypeFaceRecord* face, uint glyphIndex, int loadFlags);

    [LibraryImport(LibraryName, EntryPoint = "FT_Render_Glyph")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int RenderGlyph(FreeTypeGlyphSlotRecord* slot, FreeTypeRenderMode renderMode);
}
