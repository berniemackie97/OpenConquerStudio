using OpenConquer.Rendering.Text.Native;

namespace OpenConquer.Rendering.Text.Fonts.Discovery;

/// <summary>
/// Discovers fonts registered with the Windows DirectWrite system font collection.
/// </summary>
internal sealed unsafe class WindowsHostFontSource : IHostFontSource
{
    public HostFontDiscovery Discover()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DirectWrite font discovery is available only on Windows.");
        }

        nint factory = 0;
        int error = DirectWriteNative.CreateSharedFactory(&factory);

        if (error < 0 || factory == 0)
        {
            throw new InvalidOperationException($"DirectWrite factory creation failed with HRESULT 0x{error:X8}.");
        }

        try
        {
            List<HostFontReference> references = DiscoverFontReferences(factory);
            HostFontReference? defaultGuiFont = ReadDefaultGuiFont(factory);
            return new HostFontDiscovery(references, defaultGuiFont);
        }
        finally
        {
            DirectWriteNative.Release(factory);
        }
    }

    private static List<HostFontReference> DiscoverFontReferences(nint factory)
    {
        nint collection = 0;
        int error = DirectWriteNative.GetSystemFontCollection(factory, &collection);

        if (error < 0 || collection == 0)
        {
            throw new InvalidOperationException($"DirectWrite system font collection lookup failed with HRESULT 0x{error:X8}.");
        }

        try
        {
            uint familyCount = DirectWriteNative.GetFontFamilyCount(collection);
            List<HostFontReference> references = [];

            for (uint familyIndex = 0; familyIndex < familyCount; familyIndex++)
            {
                AddFamilyReferences(collection, familyIndex, references);
            }

            return references;
        }
        finally
        {
            DirectWriteNative.Release(collection);
        }
    }

    private static void AddFamilyReferences(nint collection, uint familyIndex, List<HostFontReference> references)
    {
        nint family = 0;
        int error = DirectWriteNative.GetFontFamily(collection, familyIndex, &family);

        if (error < 0 || family == 0)
        {
            return;
        }

        try
        {
            uint fontCount = DirectWriteNative.GetFontCount(family);

            for (uint fontIndex = 0; fontIndex < fontCount; fontIndex++)
            {
                AddFontReference(family, fontIndex, references);
            }
        }
        finally
        {
            DirectWriteNative.Release(family);
        }
    }

    private static void AddFontReference(nint family, uint fontIndex, List<HostFontReference> references)
    {
        nint font = 0;
        int error = DirectWriteNative.GetFont(family, fontIndex, &font);

        if (error < 0 || font == 0)
        {
            return;
        }

        try
        {
            HostFontReference? reference = ReadFontReference(font);

            if (reference is not null)
            {
                references.Add(reference);
            }
        }
        finally
        {
            DirectWriteNative.Release(font);
        }
    }

    private static HostFontReference? ReadFontReference(nint font)
    {
        nint face = 0;
        int error = DirectWriteNative.CreateFontFace(font, &face);

        if (error < 0 || face == 0)
        {
            return null;
        }

        try
        {
            return ReadFaceReference(face);
        }
        finally
        {
            DirectWriteNative.Release(face);
        }
    }

    private static HostFontReference? ReadFaceReference(nint face)
    {
        uint fileCount = 0;
        int error = DirectWriteNative.GetFontFiles(face, &fileCount, null);

        if (error < 0 || fileCount != 1)
        {
            return null;
        }

        nint fontFile = 0;
        uint capacity = 1;
        error = DirectWriteNative.GetFontFiles(face, &capacity, &fontFile);

        if (error < 0 || capacity != 1 || fontFile == 0)
        {
            if (fontFile != 0)
            {
                DirectWriteNative.Release(fontFile);
            }

            return null;
        }

        try
        {
            uint faceIndex = DirectWriteNative.GetFontFaceIndex(face);

            if (faceIndex > int.MaxValue)
            {
                return null;
            }

            string? filePath = ReadLocalFontFilePath(fontFile);
            return string.IsNullOrWhiteSpace(filePath) ? null : new HostFontReference(filePath, (int)faceIndex);
        }
        finally
        {
            DirectWriteNative.Release(fontFile);
        }
    }

    private static string? ReadLocalFontFilePath(nint fontFile)
    {
        nint referenceKey = 0;
        uint referenceKeySize = 0;
        int error = DirectWriteNative.GetFontFileReferenceKey(fontFile, &referenceKey, &referenceKeySize);

        if (error < 0 || referenceKey == 0 || referenceKeySize == 0)
        {
            return null;
        }

        nint loader = 0;
        error = DirectWriteNative.GetFontFileLoader(fontFile, &loader);

        if (error < 0 || loader == 0)
        {
            return null;
        }

        try
        {
            nint localLoader = 0;
            error = DirectWriteNative.QueryLocalFontFileLoader(loader, &localLoader);

            if (error < 0 || localLoader == 0)
            {
                return null;
            }

            try
            {
                return ReadLocalFontFilePath(localLoader, referenceKey, referenceKeySize);
            }
            finally
            {
                DirectWriteNative.Release(localLoader);
            }
        }
        finally
        {
            DirectWriteNative.Release(loader);
        }
    }

    private static string? ReadLocalFontFilePath(nint localLoader, nint referenceKey, uint referenceKeySize)
    {
        uint pathLength = 0;
        int error = DirectWriteNative.GetFilePathLength(localLoader, referenceKey, referenceKeySize, &pathLength);

        if (error < 0 || pathLength == 0 || pathLength >= int.MaxValue)
        {
            return null;
        }

        char[] path = new char[checked((int)pathLength + 1)];

        fixed (char* pathBuffer = path)
        {
            error = DirectWriteNative.GetFilePath(localLoader, referenceKey, referenceKeySize, pathBuffer, checked(pathLength + 1));

            if (error < 0)
            {
                return null;
            }

            return new string(pathBuffer, 0, checked((int)pathLength));
        }
    }

    private static HostFontReference? ReadDefaultGuiFont(nint factory)
    {
        nint stockFont = GdiNative.GetStockObject(GdiNative.DefaultGuiFont);

        if (stockFont == 0)
        {
            return null;
        }

        LogFontW logFont = default;
        int objectSize = sizeof(LogFontW);

        if (GdiNative.GetObject(stockFont, objectSize, &logFont) != objectSize)
        {
            return null;
        }

        nint gdiInterop = 0;
        int error = DirectWriteNative.GetGdiInterop(factory, &gdiInterop);

        if (error < 0 || gdiInterop == 0)
        {
            return null;
        }

        try
        {
            nint font = 0;
            error = DirectWriteNative.CreateFontFromLogFont(gdiInterop, &logFont, &font);

            if (error < 0 || font == 0)
            {
                return null;
            }

            try
            {
                return ReadFontReference(font);
            }
            finally
            {
                DirectWriteNative.Release(font);
            }
        }
        finally
        {
            DirectWriteNative.Release(gdiInterop);
        }
    }
}
