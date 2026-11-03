using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OpenConquer.Rendering.Text.Native;

internal static unsafe partial class CoreFoundationNative
{
    internal const int PosixPathStyle = 0;
    internal const uint Utf8Encoding = 0x08000100;

    private const string LibraryName = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [LibraryImport(LibraryName, EntryPoint = "CFRelease")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial void Release(nint value);

    [LibraryImport(LibraryName, EntryPoint = "CFArrayGetCount")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nint ArrayGetCount(nint array);

    [LibraryImport(LibraryName, EntryPoint = "CFArrayGetValueAtIndex")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nint ArrayGetValueAtIndex(nint array, nint index);

    [LibraryImport(LibraryName, EntryPoint = "CFURLCopyFileSystemPath")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nint UrlCopyFileSystemPath(nint url, int pathStyle);

    [LibraryImport(LibraryName, EntryPoint = "CFStringGetLength")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nint StringGetLength(nint value);

    [LibraryImport(LibraryName, EntryPoint = "CFStringGetMaximumSizeForEncoding")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nint StringGetMaximumSizeForEncoding(nint length, uint encoding);

    [LibraryImport(LibraryName, EntryPoint = "CFStringGetCString")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial byte StringGetCString(nint value, byte* buffer, nint bufferSize, uint encoding);
}
