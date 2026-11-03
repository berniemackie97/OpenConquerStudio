using System.Runtime.InteropServices;
using OpenConquer.Rendering.Text.Native;

namespace OpenConquer.Rendering.Text.Fonts.FreeType;

/// <summary>
/// Reads font face metadata through FreeType for host font resolution.
/// </summary>
internal sealed unsafe class FreeTypeFontInspector
{
    private const int MaximumFaceCount = ushort.MaxValue + 1;

    private readonly FreeTypeLibrary _library;

    public FreeTypeFontInspector(FreeTypeLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        _library = library;
    }

    public FaceInfo[] ReadFaces(FreeTypeFontFile fontFile)
    {
        ArgumentNullException.ThrowIfNull(fontFile);

        byte* fontData = fontFile.Data;
        int fontDataLength = fontFile.Length;

        return _library.UseHandle(library => ReadFaces(library, fontData, fontDataLength));
    }

    internal FaceInfo[] ReadFaces(ReadOnlySpan<byte> fontData)
    {
        if (fontData.IsEmpty)
        {
            return [];
        }

        fixed (byte* data = fontData)
        {
            nint dataAddress = (nint)data;
            int dataLength = fontData.Length;

            return _library.UseHandle(library => ReadFaces(library, (byte*)dataAddress, dataLength));
        }
    }

    private static FaceInfo[] ReadFaces(nint library, byte* fontData, int fontDataLength)
    {
        if (!TryReadFace(
                library, fontData, fontDataLength, faceIndex: 0,
                out int faceCount, out string? familyName, out bool isBold, out bool isItalic))
        {
            return [];
        }

        if (faceCount is < 1 or > MaximumFaceCount)
        {
            return [];
        }

        List<FaceInfo> faces = [];

        if (!string.IsNullOrWhiteSpace(familyName))
        {
            faces.Add(new FaceInfo(faceIndex: 0, familyName, isBold, isItalic));
        }

        for (int faceIndex = 1; faceIndex < faceCount; faceIndex++)
        {
            if (!TryReadFace(
                    library, fontData, fontDataLength, faceIndex,
                    out _, out familyName, out isBold, out isItalic))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(familyName))
            {
                faces.Add(new FaceInfo(faceIndex, familyName, isBold, isItalic));
            }
        }

        return faces.ToArray();
    }

    private static bool TryReadFace(
        nint library, byte* fontData, int fontDataLength, int faceIndex,
        out int faceCount, out string? familyName, out bool isBold, out bool isItalic)
    {
        FreeTypeFaceRecord* face = null;
        int error = FreeTypeNative.NewMemoryFace(
            library, fontData, new CLong(fontDataLength), new CLong(faceIndex), &face);

        if (error != 0 || face is null)
        {
            if (face is not null)
            {
                _ = FreeTypeNative.DoneFace(face);
            }

            faceCount = 0;
            familyName = null;
            isBold = false;
            isItalic = false;
            return false;
        }

        int releaseError = 0;

        try
        {
            long nativeFaceCount = face->FaceCount.Value;
            long styleFlags = face->StyleFlags.Value;

            faceCount = nativeFaceCount is >= 1 and <= MaximumFaceCount ? (int)nativeFaceCount : 0;
            familyName = face->FamilyName is null ? null : Marshal.PtrToStringUTF8((nint)face->FamilyName);
            isBold = (styleFlags & (int)FreeTypeStyleFlags.Bold) != 0;
            isItalic = (styleFlags & (int)FreeTypeStyleFlags.Italic) != 0;
        }
        finally
        {
            releaseError = FreeTypeNative.DoneFace(face);
        }

        if (releaseError != 0)
        {
            throw new InvalidOperationException(
                $"FreeType could not release inspected face {faceIndex} (error {releaseError}).");
        }

        return true;
    }

    internal sealed class FaceInfo
    {
        public FaceInfo(int faceIndex, string familyName)
            : this(faceIndex, familyName, isBold: false, isItalic: false)
        {
        }

        public FaceInfo(int faceIndex, string familyName, bool isBold, bool isItalic)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(faceIndex);
            ArgumentException.ThrowIfNullOrWhiteSpace(familyName);

            FaceIndex = faceIndex;
            FamilyName = familyName;
            IsBold = isBold;
            IsItalic = isItalic;
        }

        public int FaceIndex
        {
            get;
        }

        public string FamilyName
        {
            get;
        }

        public bool IsBold
        {
            get;
        }

        public bool IsItalic
        {
            get;
        }
    }
}
