using System.Runtime.InteropServices;
using OpenConquer.Rendering.Text.Native;

namespace OpenConquer.Rendering.Text.Fonts.Discovery;

/// <summary>
/// Discovers fonts registered with the Linux Fontconfig configuration.
/// </summary>
internal sealed unsafe class LinuxHostFontSource : IHostFontSource
{
    private static ReadOnlySpan<byte> FileProperty => "file\0"u8;
    private static ReadOnlySpan<byte> IndexProperty => "index\0"u8;
    private static ReadOnlySpan<byte> DefaultGuiPattern => "sans-serif\0"u8;

    public HostFontDiscovery Discover()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Fontconfig font discovery is available only on Linux.");
        }

        nint config = FontConfigNative.InitLoadConfigAndFonts();

        if (config == 0)
        {
            throw new InvalidOperationException("Fontconfig failed to load the system configuration and fonts.");
        }

        try
        {
            string? systemRoot = ReadUtf8(FontConfigNative.ConfigGetSysRoot(config));
            List<HostFontReference> references = ReadFontReferences(config, systemRoot);
            HostFontReference? defaultGuiFont = ReadDefaultGuiFont(config, systemRoot);
            return new HostFontDiscovery(references, defaultGuiFont);
        }
        finally
        {
            FontConfigNative.ConfigDestroy(config);
        }
    }

    private static List<HostFontReference> ReadFontReferences(nint config, string? sysRoot)
    {
        FontConfigFontSet* fontSet = FontConfigNative.ConfigGetFonts(config, FontConfigNative.SystemFontSet);

        if (fontSet == null)
        {
            throw new InvalidOperationException("Fontconfig did not return the system font set.");
        }

        if (fontSet->FontCount < 0 || (fontSet->FontCount > 0 && fontSet->Fonts == null))
        {
            throw new InvalidOperationException("Fontconfig returned an invalid system font set.");
        }

        List<HostFontReference> references = new(fontSet->FontCount);

        for (int index = 0; index < fontSet->FontCount; index++)
        {
            nint pattern = fontSet->Fonts[index];

            if (pattern == 0)
            {
                continue;
            }

            HostFontReference? reference = ReadFontReference(pattern, sysRoot);

            if (reference is not null)
            {
                references.Add(reference);
            }
        }

        return references;
    }

    private static HostFontReference? ReadDefaultGuiFont(nint config, string? sysRoot)
    {
        nint pattern;

        fixed (byte* patternName = DefaultGuiPattern)
        {
            pattern = FontConfigNative.NameParse(patternName);
        }

        if (pattern == 0)
        {
            return null;
        }

        try
        {
            if (FontConfigNative.ConfigSubstitute(config, pattern, FontConfigNative.MatchPattern) == 0)
            {
                return null;
            }

            FontConfigNative.DefaultSubstitute(pattern);

            int result = -1;
            nint match = FontConfigNative.FontMatch(config, pattern, &result);

            if (match == 0)
            {
                return null;
            }

            try
            {
                return result == FontConfigNative.ResultMatch ? ReadFontReference(match, sysRoot) : null;
            }
            finally
            {
                FontConfigNative.PatternDestroy(match);
            }
        }
        finally
        {
            FontConfigNative.PatternDestroy(pattern);
        }
    }

    private static HostFontReference? ReadFontReference(nint pattern, string? sysRoot)
    {
        byte* nativeFilePath = null;

        fixed (byte* propertyName = FileProperty)
        {
            if (FontConfigNative.PatternGetString(pattern, propertyName, 0, &nativeFilePath) != FontConfigNative.ResultMatch
                || nativeFilePath == null)
            {
                return null;
            }
        }

        string? filePath = ReadUtf8(nativeFilePath);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        int faceIndex = 0;

        fixed (byte* propertyName = IndexProperty)
        {
            if (FontConfigNative.PatternGetInteger(pattern, propertyName, 0, &faceIndex) != FontConfigNative.ResultMatch
                || faceIndex < 0)
            {
                return null;
            }
        }

        string? resolvedPath = ResolvePath(filePath, sysRoot);
        return string.IsNullOrWhiteSpace(resolvedPath) ? null : new HostFontReference(resolvedPath, faceIndex);
    }

    private static string? ResolvePath(string filePath, string? sysRoot)
    {
        if (string.IsNullOrEmpty(sysRoot))
        {
            return filePath;
        }

        string relativePath = filePath.TrimStart(Path.DirectorySeparatorChar);

        if (relativePath.Length == 0)
        {
            return null;
        }

        return Path.Combine(sysRoot, relativePath);
    }

    private static string? ReadUtf8(byte* value)
    {
        return value == null ? null : Marshal.PtrToStringUTF8((nint)value);
    }
}
