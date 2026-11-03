using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OpenConquer.Rendering.Text.Native;

internal static unsafe partial class FontConfigNative
{
    internal const int SystemFontSet = 0;
    internal const int MatchPattern = 0;
    internal const int ResultMatch = 0;

    private const string LibraryName = "libfontconfig.so.1";

    [LibraryImport(LibraryName, EntryPoint = "FcInitLoadConfigAndFonts")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nint InitLoadConfigAndFonts();

    [LibraryImport(LibraryName, EntryPoint = "FcConfigDestroy")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial void ConfigDestroy(nint config);

    [LibraryImport(LibraryName, EntryPoint = "FcConfigGetFonts")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial FontConfigFontSet* ConfigGetFonts(nint config, int set);

    [LibraryImport(LibraryName, EntryPoint = "FcConfigGetSysRoot")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial byte* ConfigGetSysRoot(nint config);

    [LibraryImport(LibraryName, EntryPoint = "FcPatternGetString")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int PatternGetString(nint pattern, byte* objectName, int index, byte** value);

    [LibraryImport(LibraryName, EntryPoint = "FcPatternGetInteger")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int PatternGetInteger(nint pattern, byte* objectName, int index, int* value);

    [LibraryImport(LibraryName, EntryPoint = "FcNameParse")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nint NameParse(byte* name);

    [LibraryImport(LibraryName, EntryPoint = "FcPatternDestroy")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial void PatternDestroy(nint pattern);

    [LibraryImport(LibraryName, EntryPoint = "FcConfigSubstitute")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int ConfigSubstitute(nint config, nint pattern, int kind);

    [LibraryImport(LibraryName, EntryPoint = "FcDefaultSubstitute")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial void DefaultSubstitute(nint pattern);

    [LibraryImport(LibraryName, EntryPoint = "FcFontMatch")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nint FontMatch(nint config, nint pattern, int* result);
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct FontConfigFontSet
{
    public int FontCount;
    public int Capacity;
    public nint* Fonts;
}
