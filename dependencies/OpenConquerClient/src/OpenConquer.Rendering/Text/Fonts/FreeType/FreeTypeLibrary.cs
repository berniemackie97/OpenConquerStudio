using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using OpenConquer.Rendering.Text.Native;

namespace OpenConquer.Rendering.Text.Fonts.FreeType;

/// <summary>
/// One initialized FreeType library instance for text rendering.
/// </summary>
internal sealed unsafe class FreeTypeLibrary : IDisposable
{
    private const int SupportedVersionMajor = 2;
    private const int MinimumVersionMinor = 14;
    private const int MinimumVersionPatch = 3;

    private readonly SafeFreeTypeLibraryHandle _handle;
    private readonly object _syncRoot = new();

    public FreeTypeLibrary()
    {
        nint library = 0;
        int error = FreeTypeNative.InitFreeType(&library);

        if (error != 0 || library == 0)
        {
            throw new InvalidOperationException($"FreeType initialization failed with error code {error}.");
        }

        SafeFreeTypeLibraryHandle handle = new(library);

        try
        {
            int major = 0;
            int minor = 0;
            int patch = 0;

            FreeTypeNative.LibraryVersion(library, &major, &minor, &patch);

            if (!IsSupportedNativeVersion(major, minor, patch))
            {
                throw new InvalidOperationException(
                    $"OpenConquer requires FreeType {SupportedVersionMajor}.{MinimumVersionMinor}.{MinimumVersionPatch} or later within the FreeType {SupportedVersionMajor}.x ABI, but loaded {major}.{minor}.{patch}.");
            }

            _handle = handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _handle.Dispose();
        }
    }

    internal T UseHandle<T>(FreeTypeLibraryOperation<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        using LibraryLease lease = AcquireLease();
        return lease.UseHandle(operation);
    }

    internal LibraryLease AcquireLease()
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_handle.IsInvalid || _handle.IsClosed, this);

            bool addedReference = false;

            try
            {
                _handle.DangerousAddRef(ref addedReference);
                return new LibraryLease(_handle, _syncRoot);
            }
            catch
            {
                if (addedReference)
                {
                    _handle.DangerousRelease();
                }

                throw;
            }
        }
    }

    internal static bool IsSupportedNativeVersion(int major, int minor, int patch)
    {
        if (major != SupportedVersionMajor)
        {
            return false;
        }

        if (minor != MinimumVersionMinor)
        {
            return minor > MinimumVersionMinor;
        }

        return patch >= MinimumVersionPatch;
    }

    internal delegate T FreeTypeLibraryOperation<T>(nint library);

    internal sealed class LibraryLease : IDisposable
    {
        private readonly object _syncRoot;
        private SafeHandle? _handle;

        internal LibraryLease(SafeHandle handle, object syncRoot)
        {
            ArgumentNullException.ThrowIfNull(handle);
            ArgumentNullException.ThrowIfNull(syncRoot);

            _handle = handle;
            _syncRoot = syncRoot;
        }

        internal T UseHandle<T>(FreeTypeLibraryOperation<T> operation)
        {
            ArgumentNullException.ThrowIfNull(operation);

            lock (_syncRoot)
            {
                ObjectDisposedException.ThrowIf(_handle is null, this);

                SafeHandle handle = _handle;
                return operation(handle.DangerousGetHandle());
            }
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                SafeHandle? handle = _handle;

                if (handle is null)
                {
                    return;
                }

                _handle = null;
                handle.DangerousRelease();
            }
        }
    }

    private sealed class SafeFreeTypeLibraryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeFreeTypeLibraryHandle(nint library) : base(ownsHandle: true)
        {
            SetHandle(library);
        }

        protected override bool ReleaseHandle()
        {
            return FreeTypeNative.DoneFreeType(handle) == 0;
        }
    }
}
