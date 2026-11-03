using OpenConquer.Rendering.Text.Fonts.FreeType;

namespace OpenConquer.Rendering.Text.Fonts.Discovery;

/// <summary>
/// Normalizes host font discovery and reads family metadata from registered font resources.
/// </summary>
internal sealed class HostFontCatalog
{
    public HostFontCatalog(FreeTypeFontInspector inspector, HostFontDiscovery discovery)
    {
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(discovery);

        StringComparer pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        Dictionary<string, FontReferenceGroup> groupsByPath = BuildGroups(discovery, pathComparer);
        List<FontReferenceGroup> groups = groupsByPath.Values.OrderBy(static group => group.FilePath, StringComparer.OrdinalIgnoreCase).ThenBy(static group => group.FilePath, StringComparer.Ordinal).ToList();

        Dictionary<string, FreeTypeFontInspector.FaceInfo[]> facesByPath = new(pathComparer);
        List<Entry> entries = BuildEntries(inspector, groups, facesByPath);

        FontFiles = Array.AsReadOnly(groups.Select(static group => group.FilePath).ToArray());
        Entries = entries.AsReadOnly();
        DefaultGuiFont = ResolveDefaultGuiFont(discovery.DefaultGuiFont, groupsByPath, facesByPath);
    }

    public IReadOnlyList<string> FontFiles
    {
        get;
    }

    public IReadOnlyList<Entry> Entries
    {
        get;
    }

    public ResolvedFont? DefaultGuiFont
    {
        get;
    }

    private static Dictionary<string, FontReferenceGroup> BuildGroups(HostFontDiscovery discovery, StringComparer pathComparer)
    {
        Dictionary<string, FontReferenceGroup> groups = new(pathComparer);

        foreach (HostFontReference reference in discovery.FontReferences)
        {
            AddReference(groups, NormalizeReference(reference));
        }

        if (discovery.DefaultGuiFont is not null)
        {
            AddReference(groups, NormalizeReference(discovery.DefaultGuiFont));
        }

        return groups;
    }

    private static void AddReference(Dictionary<string, FontReferenceGroup> groups, HostFontReference reference)
    {
        if (!groups.TryGetValue(reference.FilePath, out FontReferenceGroup? group))
        {
            group = new FontReferenceGroup(reference.FilePath);
            groups.Add(reference.FilePath, group);
        }

        group.Add(reference);
    }

    private static HostFontReference NormalizeReference(HostFontReference reference)
    {
        return new HostFontReference(Path.GetFullPath(reference.FilePath), reference.FaceIndex);
    }

    private static List<Entry> BuildEntries(
        FreeTypeFontInspector inspector,
        IReadOnlyList<FontReferenceGroup> groups,
        Dictionary<string, FreeTypeFontInspector.FaceInfo[]> facesByPath)
    {
        List<Entry> entries = [];

        foreach (FontReferenceGroup group in groups)
        {
            FreeTypeFontInspector.FaceInfo[] faces;

            try
            {
                using FreeTypeFontFile mappedFont = new(group.FilePath);
                faces = inspector.ReadFaces(mappedFont);
            }
            catch (InvalidDataException)
            {
                faces = [];
            }
            catch (IOException)
            {
                faces = [];
            }
            catch (UnauthorizedAccessException)
            {
                faces = [];
            }

            facesByPath.Add(group.FilePath, faces);

            foreach (FreeTypeFontInspector.FaceInfo face in faces)
            {
                if (!group.IncludesAllFaces && !group.FaceIndices.Contains(face.FaceIndex))
                {
                    continue;
                }

                entries.Add(new Entry(new ResolvedFont(group.FilePath, face.FaceIndex), face.FamilyName, face.IsBold, face.IsItalic));
            }
        }

        return entries;
    }

    private static ResolvedFont? ResolveDefaultGuiFont(HostFontReference? defaultGuiFont, IReadOnlyDictionary<string, FontReferenceGroup> groupsByPath, Dictionary<string, FreeTypeFontInspector.FaceInfo[]> facesByPath)
    {
        if (defaultGuiFont is null)
        {
            return null;
        }

        HostFontReference normalized = NormalizeReference(defaultGuiFont);

        if (!groupsByPath.TryGetValue(normalized.FilePath, out FontReferenceGroup? group))
        {
            return null;
        }

        if (normalized.FaceIndex is { } faceIndex)
        {
            return new ResolvedFont(group.FilePath, faceIndex);
        }

        if (!facesByPath.TryGetValue(group.FilePath, out FreeTypeFontInspector.FaceInfo[]? faces) || faces.Length == 0)
        {
            return null;
        }

        FreeTypeFontInspector.FaceInfo selectedFace = faces[0];

        foreach (FreeTypeFontInspector.FaceInfo face in faces)
        {
            if (!face.IsBold && !face.IsItalic)
            {
                selectedFace = face;
                break;
            }
        }

        return new ResolvedFont(group.FilePath, selectedFace.FaceIndex);
    }

    internal sealed class Entry
    {
        public Entry(ResolvedFont font, string familyName) : this(font, familyName, isBold: false, isItalic: false)
        {
        }

        public Entry(ResolvedFont font, string familyName, bool isBold, bool isItalic)
        {
            ArgumentNullException.ThrowIfNull(font);
            ArgumentException.ThrowIfNullOrWhiteSpace(familyName);

            Font = font;
            FamilyName = familyName;
            IsBold = isBold;
            IsItalic = isItalic;
        }

        public ResolvedFont Font
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

    private sealed class FontReferenceGroup(string filePath)
    {
        public string FilePath
        {
            get; private set;
        } = filePath;

        public bool IncludesAllFaces
        {
            get; private set;
        }

        public HashSet<int> FaceIndices { get; } = [];

        public void Add(HostFontReference reference)
        {
            if (string.CompareOrdinal(reference.FilePath, FilePath) < 0)
            {
                FilePath = reference.FilePath;
            }

            if (reference.FaceIndex is null)
            {
                IncludesAllFaces = true;
                FaceIndices.Clear();
                return;
            }

            if (!IncludesAllFaces)
            {
                FaceIndices.Add(reference.FaceIndex.Value);
            }
        }
    }
}
