using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OpenConquer.Rendering.Text.Native;

internal static unsafe partial class DirectWriteNative
{
    private const string LibraryName = "dwrite.dll";
    private const uint SharedFactory = 0;

    private static readonly Guid s_factoryId = new("B859EE5A-D838-4B5B-A2E8-1ADC7D93DB48");
    private static readonly Guid s_localFontFileLoaderId = new("B2D9F3EC-C9FE-4A11-A2EC-D86208F7C0A2");

    [LibraryImport(LibraryName, EntryPoint = "DWriteCreateFactory")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static partial int CreateFactory(uint factoryType, Guid* interfaceId, nint* factory);

    internal static int CreateSharedFactory(nint* factory)
    {
        Guid interfaceId = s_factoryId;
        return CreateFactory(SharedFactory, &interfaceId, factory);
    }

    internal static int QueryLocalFontFileLoader(nint loader, nint* localLoader)
    {
        Guid interfaceId = s_localFontFileLoaderId;
        return QueryInterface(loader, &interfaceId, localLoader);
    }

    internal static uint Release(nint instance)
    {
        if (instance == 0)
        {
            return 0;
        }

        nint* vtable = *(nint**)instance;
        delegate* unmanaged[Stdcall]<nint, uint> release = (delegate* unmanaged[Stdcall]<nint, uint>)vtable[2];
        return release(instance);
    }

    internal static int GetSystemFontCollection(nint factory, nint* collection)
    {
        nint* vtable = *(nint**)factory;
        delegate* unmanaged[Stdcall]<nint, nint*, int, int> method = (delegate* unmanaged[Stdcall]<nint, nint*, int, int>)vtable[3];
        return method(factory, collection, 0);
    }

    internal static int GetGdiInterop(nint factory, nint* gdiInterop)
    {
        nint* vtable = *(nint**)factory;
        delegate* unmanaged[Stdcall]<nint, nint*, int> method = (delegate* unmanaged[Stdcall]<nint, nint*, int>)vtable[17];
        return method(factory, gdiInterop);
    }

    internal static uint GetFontFamilyCount(nint collection)
    {
        nint* vtable = *(nint**)collection;
        delegate* unmanaged[Stdcall]<nint, uint> method = (delegate* unmanaged[Stdcall]<nint, uint>)vtable[3];
        return method(collection);
    }

    internal static int GetFontFamily(nint collection, uint index, nint* family)
    {
        nint* vtable = *(nint**)collection;
        delegate* unmanaged[Stdcall]<nint, uint, nint*, int> method = (delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)vtable[4];
        return method(collection, index, family);
    }

    internal static uint GetFontCount(nint fontList)
    {
        nint* vtable = *(nint**)fontList;
        delegate* unmanaged[Stdcall]<nint, uint> method = (delegate* unmanaged[Stdcall]<nint, uint>)vtable[4];
        return method(fontList);
    }

    internal static int GetFont(nint fontList, uint index, nint* font)
    {
        nint* vtable = *(nint**)fontList;
        delegate* unmanaged[Stdcall]<nint, uint, nint*, int> method = (delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)vtable[5];
        return method(fontList, index, font);
    }

    internal static int CreateFontFace(nint font, nint* face)
    {
        nint* vtable = *(nint**)font;
        delegate* unmanaged[Stdcall]<nint, nint*, int> method = (delegate* unmanaged[Stdcall]<nint, nint*, int>)vtable[13];
        return method(font, face);
    }

    internal static int CreateFontFromLogFont(nint gdiInterop, LogFontW* logFont, nint* font)
    {
        nint* vtable = *(nint**)gdiInterop;
        delegate* unmanaged[Stdcall]<nint, LogFontW*, nint*, int> method = (delegate* unmanaged[Stdcall]<nint, LogFontW*, nint*, int>)vtable[3];

        return method(gdiInterop, logFont, font);
    }

    internal static int GetFontFiles(nint face, uint* numberOfFiles, nint* fontFiles)
    {
        nint* vtable = *(nint**)face;
        delegate* unmanaged[Stdcall]<nint, uint*, nint*, int> method = (delegate* unmanaged[Stdcall]<nint, uint*, nint*, int>)vtable[4];

        return method(face, numberOfFiles, fontFiles);
    }

    internal static uint GetFontFaceIndex(nint face)
    {
        nint* vtable = *(nint**)face;
        delegate* unmanaged[Stdcall]<nint, uint> method = (delegate* unmanaged[Stdcall]<nint, uint>)vtable[5];
        return method(face);
    }

    internal static int GetFontFileReferenceKey(nint fontFile, nint* referenceKey, uint* referenceKeySize)
    {
        nint* vtable = *(nint**)fontFile;
        delegate* unmanaged[Stdcall]<nint, nint*, uint*, int> method = (delegate* unmanaged[Stdcall]<nint, nint*, uint*, int>)vtable[3];

        return method(fontFile, referenceKey, referenceKeySize);
    }

    internal static int GetFontFileLoader(nint fontFile, nint* loader)
    {
        nint* vtable = *(nint**)fontFile;
        delegate* unmanaged[Stdcall]<nint, nint*, int> method = (delegate* unmanaged[Stdcall]<nint, nint*, int>)vtable[4];
        return method(fontFile, loader);
    }

    internal static int GetFilePathLength(nint loader, nint referenceKey, uint referenceKeySize, uint* pathLength)
    {
        nint* vtable = *(nint**)loader;
        delegate* unmanaged[Stdcall]<nint, nint, uint, uint*, int> method = (delegate* unmanaged[Stdcall]<nint, nint, uint, uint*, int>)vtable[4];

        return method(loader, referenceKey, referenceKeySize, pathLength);
    }

    internal static int GetFilePath(nint loader, nint referenceKey, uint referenceKeySize, char* path, uint pathSize)
    {
        nint* vtable = *(nint**)loader;
        delegate* unmanaged[Stdcall]<nint, nint, uint, char*, uint, int> method = (delegate* unmanaged[Stdcall]<nint, nint, uint, char*, uint, int>)vtable[5];

        return method(loader, referenceKey, referenceKeySize, path, pathSize);
    }

    private static int QueryInterface(nint instance, Guid* interfaceId, nint* result)
    {
        nint* vtable = *(nint**)instance;
        delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int> method = (delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)vtable[0];

        return method(instance, interfaceId, result);
    }
}
