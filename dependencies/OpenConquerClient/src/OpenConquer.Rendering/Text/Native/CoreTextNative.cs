using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OpenConquer.Rendering.Text.Native;

internal static unsafe partial class CoreTextNative
{
    internal const uint SystemUiFontType = 2;

    private const string LibraryName = "/System/Library/Frameworks/CoreText.framework/CoreText";

    private static readonly nint s_libraryHandle = NativeLibrary.Load(LibraryName);

    internal static nint FontUrlAttribute { get; } = GetExportedCoreFoundationObject("kCTFontURLAttribute");

    [LibraryImport(LibraryName, EntryPoint = "CTFontManagerCopyAvailableFontURLs")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nint FontManagerCopyAvailableFontUrls();

    [LibraryImport(LibraryName, EntryPoint = "CTFontCreateUIFontForLanguage")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nint FontCreateUiFontForLanguage(uint uiType, double size, nint language);

    [LibraryImport(LibraryName, EntryPoint = "CTFontCopyAttribute")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nint FontCopyAttribute(nint font, nint attribute);

    private static nint GetExportedCoreFoundationObject(string symbolName)
    {
        nint symbol = NativeLibrary.GetExport(s_libraryHandle, symbolName);
        nint value = *(nint*)symbol;

        if (value == 0)
        {
            throw new InvalidOperationException($"CoreText exported a null value for {symbolName}.");
        }

        return value;
    }
}
