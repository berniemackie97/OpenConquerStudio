using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OpenConquer.Rendering.Text.Native;

internal static unsafe partial class GdiNative
{
    internal const int DefaultGuiFont = 17;
    internal const int LogicalFontFaceNameLength = 32;

    private const string LibraryName = "gdi32.dll";

    [LibraryImport(LibraryName, EntryPoint = "GetStockObject")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    internal static partial nint GetStockObject(int objectType);

    [LibraryImport(LibraryName, EntryPoint = "GetObjectW")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    internal static partial int GetObject(nint handle, int objectSize, LogFontW* objectData);
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct LogFontW
{
    public int Height;
    public int Width;
    public int Escapement;
    public int Orientation;
    public int Weight;
    public byte Italic;
    public byte Underline;
    public byte StrikeOut;
    public byte CharacterSet;
    public byte OutputPrecision;
    public byte ClipPrecision;
    public byte Quality;
    public byte PitchAndFamily;
    public fixed char FaceName[GdiNative.LogicalFontFaceNameLength];
}
