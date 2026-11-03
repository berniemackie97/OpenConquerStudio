using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using OpenConquer.Rendering.Text.Native;

namespace OpenConquer.Rendering.Text.Fonts.FreeType;

/// <summary>
/// One FreeType face and the mapped font data behind it.
/// </summary>
internal sealed unsafe class FreeTypeFace : IDisposable
{
    private readonly SafeFreeTypeFaceHandle _handle;
    private readonly object _syncRoot = new();

    public FreeTypeFace(FreeTypeLibrary library, ResolvedFont font)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(font);

        _handle = CreateHandle(library, font);
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _handle.Dispose();
        }
    }

    internal T UseHandle<T>(FreeTypeFaceOperation<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_handle.IsClosed || _handle.IsInvalid, this);
            return operation(_handle.DangerousGetHandle());
        }
    }

    internal delegate T FreeTypeFaceOperation<T>(nint face);

    private static SafeFreeTypeFaceHandle CreateHandle(FreeTypeLibrary library, ResolvedFont font)
    {
        FreeTypeFontFile fontFile = new(font.FilePath);
        FreeTypeLibrary.LibraryLease libraryLease;

        try
        {
            libraryLease = library.AcquireLease();
        }
        catch
        {
            fontFile.Dispose();
            throw;
        }

        try
        {
            nint faceAddress = libraryLease.UseHandle(libraryHandle =>
            {
                FreeTypeFaceRecord* face = null;
                int error = FreeTypeNative.NewMemoryFace(libraryHandle, fontFile.Data, new CLong(fontFile.Length), new CLong(font.FaceIndex), &face);

                if (error == 0 && face is not null)
                {
                    return (nint)face;
                }

                if (face is not null)
                {
                    _ = FreeTypeNative.DoneFace(face);
                }

                throw new FreeTypeFaceCreationException($"FreeType could not open face {font.FaceIndex} from font '{font.FilePath}' (error {error}).");
            });

            try
            {
                return new SafeFreeTypeFaceHandle(faceAddress, libraryLease, fontFile);
            }
            catch
            {
                _ = libraryLease.UseHandle(_ => FreeTypeNative.DoneFace((FreeTypeFaceRecord*)faceAddress));

                throw;
            }
        }
        catch
        {
            libraryLease.Dispose();
            fontFile.Dispose();
            throw;
        }
    }

    private sealed class SafeFreeTypeFaceHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private readonly FreeTypeLibrary.LibraryLease _libraryLease;
        private readonly FreeTypeFontFile _fontFile;

        public SafeFreeTypeFaceHandle(
            nint face,
            FreeTypeLibrary.LibraryLease libraryLease,
            FreeTypeFontFile fontFile)
            : base(ownsHandle: true)
        {
            ArgumentNullException.ThrowIfNull(libraryLease);
            ArgumentNullException.ThrowIfNull(fontFile);

            _libraryLease = libraryLease;
            _fontFile = fontFile;
            SetHandle(face);
        }

        protected override bool ReleaseHandle()
        {
            int error;

            try
            {
                error = _libraryLease.UseHandle(
                    _ => FreeTypeNative.DoneFace((FreeTypeFaceRecord*)handle));
            }
            catch (ObjectDisposedException)
            {
                error = -1;
            }
            finally
            {
                _fontFile.Dispose();
                _libraryLease.Dispose();
            }

            return error == 0;
        }
    }
}
