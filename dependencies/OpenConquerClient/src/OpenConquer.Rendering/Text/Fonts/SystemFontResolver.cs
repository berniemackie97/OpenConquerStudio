using System.Collections.Frozen;
using OpenConquer.Rendering.Text.Fonts.Discovery;

namespace OpenConquer.Rendering.Text.Fonts;

/// <summary>
/// Resolves font tokens against discovered host fonts.
/// </summary>
internal sealed class SystemFontResolver : IFontResolver
{
    private readonly FrozenDictionary<string, string> _fontFilesByName;
    private readonly FrozenDictionary<string, ResolvedFont> _fontsByFamilyName;
    private readonly FrozenDictionary<string, ResolvedFont> _boldFontsByFamilyName;
    private readonly ResolvedFont? _defaultGuiFont;

    public SystemFontResolver(HostFontCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        _fontFilesByName = BuildFontFileIndex(catalog.FontFiles);
        _fontsByFamilyName = BuildFamilyIndex(catalog.Entries);
        _boldFontsByFamilyName = BuildBoldFamilyIndex(catalog.Entries);
        _defaultGuiFont = catalog.DefaultGuiFont;
    }

    internal SystemFontResolver(IReadOnlyList<string> fontFiles, IReadOnlyList<HostFontCatalog.Entry> entries, ResolvedFont? defaultGuiFont = null)
    {
        ArgumentNullException.ThrowIfNull(fontFiles);
        ArgumentNullException.ThrowIfNull(entries);

        _fontFilesByName = BuildFontFileIndex(fontFiles);
        _fontsByFamilyName = BuildFamilyIndex(entries);
        _boldFontsByFamilyName = BuildBoldFamilyIndex(entries);
        _defaultGuiFont = defaultGuiFont;
    }

    public bool TryResolve(string fontToken, out ResolvedFont? font)
    {
        ArgumentNullException.ThrowIfNull(fontToken);

        if (fontToken.Length == 0)
        {
            font = null;
            return false;
        }

        if (fontToken[0] == '$')
        {
            return TryResolveFontFile(fontToken.AsSpan(1), out font);
        }

        return _fontsByFamilyName.TryGetValue(fontToken, out font);
    }

    public bool TryResolveBold(string familyName, out ResolvedFont? font)
    {
        ArgumentNullException.ThrowIfNull(familyName);
        return _boldFontsByFamilyName.TryGetValue(familyName, out font);
    }

    public bool TryResolveDefaultGuiFont(out ResolvedFont? font)
    {
        font = _defaultGuiFont;
        return font is not null;
    }

    private bool TryResolveFontFile(ReadOnlySpan<char> fileName, out ResolvedFont? font)
    {
        if (!IsSupportedFontFileName(fileName))
        {
            font = null;
            return false;
        }

        if (!_fontFilesByName.TryGetValue(fileName.ToString(), out string? filePath))
        {
            font = null;
            return false;
        }

        font = new ResolvedFont(filePath, faceIndex: 0);
        return true;
    }

    private static FrozenDictionary<string, string> BuildFontFileIndex(IReadOnlyList<string> fontFiles)
    {
        Dictionary<string, string> index = new(StringComparer.OrdinalIgnoreCase);

        foreach (string filePath in fontFiles)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            string fileName = Path.GetFileName(filePath.AsSpan()).ToString();

            if (fileName.Length == 0)
            {
                throw new ArgumentException($"Font file path '{filePath}' does not contain a file name.", nameof(fontFiles));
            }

            index.TryAdd(fileName, filePath);
        }

        return index.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static FrozenDictionary<string, ResolvedFont> BuildFamilyIndex(IReadOnlyList<HostFontCatalog.Entry> entries)
    {
        Dictionary<string, HostFontCatalog.Entry> selectedEntries = new(StringComparer.OrdinalIgnoreCase);

        foreach (HostFontCatalog.Entry entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);

            if (!selectedEntries.TryGetValue(entry.FamilyName, out HostFontCatalog.Entry? selectedEntry) || IsRegular(entry) && !IsRegular(selectedEntry))
            {
                selectedEntries[entry.FamilyName] = entry;
            }
        }

        Dictionary<string, ResolvedFont> index = new(selectedEntries.Count, StringComparer.OrdinalIgnoreCase);

        foreach ((string familyName, HostFontCatalog.Entry entry) in selectedEntries)
        {
            index.Add(familyName, entry.Font);
        }

        return index.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static FrozenDictionary<string, ResolvedFont> BuildBoldFamilyIndex(IReadOnlyList<HostFontCatalog.Entry> entries)
    {
        Dictionary<string, ResolvedFont> index = new(StringComparer.OrdinalIgnoreCase);

        foreach (HostFontCatalog.Entry entry in entries)
        {
            if (entry.IsBold && !entry.IsItalic)
            {
                index.TryAdd(entry.FamilyName, entry.Font);
            }
        }

        return index.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsSupportedFontFileName(ReadOnlySpan<char> fileName)
    {
        if (fileName.IsEmpty || fileName.IndexOfAny('/', '\\') >= 0)
        {
            return false;
        }

        int extensionIndex = fileName.LastIndexOf('.');

        if (extensionIndex <= 0 || fileName.Length - extensionIndex != 4)
        {
            return false;
        }

        return fileName[extensionIndex + 1] is 't' or 'T' && fileName[extensionIndex + 2] is 't' or 'T';
    }

    private static bool IsRegular(HostFontCatalog.Entry entry)
    {
        return !entry.IsBold && !entry.IsItalic;
    }
}
