using System.IO.MemoryMappedFiles;

namespace OpenConquer.Rendering.Text.Fonts.FreeType;

/// <summary>
/// A read-only memory mapping of a font file, used as the data behind a FreeType memory face.
/// </summary>
internal sealed unsafe class FreeTypeFontFile : IDisposable
{
    private readonly MemoryMappedFile _mappedFile;
    private readonly MemoryMappedViewAccessor _view;
    private byte* _data;
    private bool _pointerAcquired;

    public FreeTypeFontFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        FileStream? stream = null;
        MemoryMappedFile? mappedFile = null;
        MemoryMappedViewAccessor? view = null;
        byte* pointer = null;
        bool pointerAcquired = false;

        try
        {
            stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);

            if (stream.Length == 0)
            {
                throw new InvalidDataException($"Font file '{filePath}' is empty.");
            }

            if (stream.Length > int.MaxValue)
            {
                throw new InvalidDataException($"Font file '{filePath}' is too large for the FreeType memory-face interface.");
            }

            int length = checked((int)stream.Length);

            mappedFile = MemoryMappedFile.CreateFromFile(stream, mapName: null, capacity: 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: false);

            stream = null;

            view = mappedFile.CreateViewAccessor(0, length, MemoryMappedFileAccess.Read);
            view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
            pointerAcquired = true;

            byte* data = (byte*)((nint)pointer + checked((nint)view.PointerOffset));

            _mappedFile = mappedFile;
            _view = view;
            _data = data;
            _pointerAcquired = true;
            Length = length;

            mappedFile = null;
            view = null;
            pointerAcquired = false;
        }
        catch
        {
            if (pointerAcquired)
            {
                view!.SafeMemoryMappedViewHandle.ReleasePointer();
            }

            view?.Dispose();
            mappedFile?.Dispose();
            stream?.Dispose();
            throw;
        }
    }

    public int Length
    {
        get;
    }

    public byte* Data
    {
        get
        {
            ObjectDisposedException.ThrowIf(!_pointerAcquired, this);
            return _data;
        }
    }

    public void Dispose()
    {
        if (!_pointerAcquired)
        {
            return;
        }

        _pointerAcquired = false;
        _data = null;

        _view.SafeMemoryMappedViewHandle.ReleasePointer();
        _view.Dispose();
        _mappedFile.Dispose();
    }
}
