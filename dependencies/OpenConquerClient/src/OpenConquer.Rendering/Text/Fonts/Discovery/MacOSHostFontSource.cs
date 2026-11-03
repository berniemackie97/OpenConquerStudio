using System.Text;
using OpenConquer.Rendering.Text.Native;

namespace OpenConquer.Rendering.Text.Fonts.Discovery;

/// <summary>
/// Discovers fonts registered with the macOS CoreText font manager.
/// </summary>
internal sealed unsafe class MacOSHostFontSource : IHostFontSource
{
    public HostFontDiscovery Discover()
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("CoreText font discovery is available only on macOS.");
        }

        nint fontUrls = CoreTextNative.FontManagerCopyAvailableFontUrls();

        if (fontUrls == 0)
        {
            throw new InvalidOperationException("CoreText did not return the available font URL collection.");
        }

        try
        {
            List<HostFontReference> references = ReadFontReferences(fontUrls);
            HostFontReference? defaultGuiFont = ReadDefaultGuiFont();
            return new HostFontDiscovery(references, defaultGuiFont);
        }
        finally
        {
            CoreFoundationNative.Release(fontUrls);
        }
    }

    private static List<HostFontReference> ReadFontReferences(nint fontUrls)
    {
        nint nativeCount = CoreFoundationNative.ArrayGetCount(fontUrls);

        if (nativeCount < 0 || nativeCount > int.MaxValue)
        {
            throw new InvalidOperationException($"CoreText returned invalid font URL count {nativeCount}.");
        }

        int count = (int)nativeCount;
        List<HostFontReference> references = new(count);

        for (int index = 0; index < count; index++)
        {
            nint fontUrl = CoreFoundationNative.ArrayGetValueAtIndex(fontUrls, index);

            if (fontUrl == 0)
            {
                continue;
            }

            string? filePath = ReadFilePath(fontUrl);

            if (!string.IsNullOrWhiteSpace(filePath))
            {
                references.Add(new HostFontReference(filePath));
            }
        }

        return references;
    }

    private static HostFontReference? ReadDefaultGuiFont()
    {
        nint font = CoreTextNative.FontCreateUiFontForLanguage(CoreTextNative.SystemUiFontType, size: 0, language: 0);

        if (font == 0)
        {
            return null;
        }

        try
        {
            nint fontUrl = CoreTextNative.FontCopyAttribute(font, CoreTextNative.FontUrlAttribute);

            if (fontUrl == 0)
            {
                return null;
            }

            try
            {
                string? filePath = ReadFilePath(fontUrl);
                return string.IsNullOrWhiteSpace(filePath) ? null : new HostFontReference(filePath);
            }
            finally
            {
                CoreFoundationNative.Release(fontUrl);
            }
        }
        finally
        {
            CoreFoundationNative.Release(font);
        }
    }

    private static string? ReadFilePath(nint fontUrl)
    {
        nint pathString = CoreFoundationNative.UrlCopyFileSystemPath(fontUrl, CoreFoundationNative.PosixPathStyle);

        if (pathString == 0)
        {
            return null;
        }

        try
        {
            return ReadString(pathString);
        }
        finally
        {
            CoreFoundationNative.Release(pathString);
        }
    }

    private static string? ReadString(nint value)
    {
        nint length = CoreFoundationNative.StringGetLength(value);

        if (length < 0)
        {
            throw new InvalidOperationException($"CoreFoundation returned invalid string length {length}.");
        }

        nint maximumByteCount = CoreFoundationNative.StringGetMaximumSizeForEncoding(length, CoreFoundationNative.Utf8Encoding);

        if (maximumByteCount < 0 || maximumByteCount >= int.MaxValue)
        {
            throw new InvalidOperationException($"CoreFoundation returned invalid UTF-8 buffer size {maximumByteCount}.");
        }

        byte[] buffer = new byte[checked((int)maximumByteCount + 1)];

        fixed (byte* bufferPointer = buffer)
        {
            byte converted = CoreFoundationNative.StringGetCString(value, bufferPointer, buffer.Length, CoreFoundationNative.Utf8Encoding);

            if (converted == 0)
            {
                return null;
            }
        }

        int byteCount = Array.IndexOf(buffer, (byte)0);

        if (byteCount < 0)
        {
            throw new InvalidOperationException("CoreFoundation returned a UTF-8 string without a terminating NUL.");
        }

        return Encoding.UTF8.GetString(buffer, 0, byteCount);
    }
}
