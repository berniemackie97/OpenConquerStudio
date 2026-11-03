namespace OpenConquer.Content.Mesh;

/// <summary>Captures the currently supported native body/idle composition without rewriting its source records.</summary>
public static class C3ModelImporter
{
    public static C3ModelData Character(IClientContentSource source, Guid id, uint packedAppearance, CancellationToken token = default) =>
        Character(source, id, packedAppearance, 0, token);

    public static C3ModelData Character(IClientContentSource source, Guid id, uint packedAppearance, uint bodyModelId, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        C3RoleCatalog catalog = C3RoleCatalog.Load(source);
        uint body = packedAppearance % 1000;
        uint selectedBody = bodyModelId == 0 ? checked(body * 1_000_000) : bodyModelId;
        if (!catalog.TryResolve("body", selectedBody, out IReadOnlyList<C3ResolvedPart> parts))
        {
            throw new InvalidDataException("The body is absent from the role catalog.");
        }
        Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
        long total = 0;
        void Capture(string path, ContentLookupMode lookup)
        {
            string normalized = C3ModelData.NormalizePath(path);
            if (files.ContainsKey(normalized))
            {
                return;
            }
            token.ThrowIfCancellationRequested();
            byte[] data = ContentReader.ReadRequiredBytes(source, path, lookup, C3MeshFile.MaximumFileBytes);
            total += data.Length;
            if (total > C3ModelData.MaximumPayloadBytes)
            {
                throw new InvalidDataException("The model source closure exceeds 64 MiB.");
            }
            files.Add(normalized, data);
        }
        Capture("ini/c3.wdb", ContentLookupMode.LooseOnly);
        foreach (string path in new[] { "ini/3dobj.dbc", "ini/3dtexture.dbc" })
        {
            if (source.TryOpenRead(path, ContentLookupMode.LooseOnly, out Stream? probe))
            {
                probe.Dispose();
                Capture(path, ContentLookupMode.LooseOnly);
            }
        }
        foreach (C3ResolvedPart part in parts)
        {
            Capture(part.MeshPath, ContentLookupMode.PackageOnly);
            Capture(part.TexturePath, ContentLookupMode.PackageOnly);
            if (part.SecondaryTextureId != 0 && part.SecondaryTexturePath is null)
            {
                throw new InvalidDataException("The model has an unresolved secondary texture dependency.");
            }
            if (part.SecondaryTexturePath is not null)
            {
                Capture(part.SecondaryTexturePath, ContentLookupMode.PackageOnly);
            }
        }
        List<C3ModelClip> clips = [];
        // WORLD-021 proves this first-world-frame lookup. Other action-key formulas are separate contracts.
        if (catalog.TryResolveMotionPath("body", body * 1_000_000UL + 100, out string motion))
        {
            Capture(motion, ContentLookupMode.PackageOnly);
            clips.Add(new C3ModelClip("Idle", C3ModelData.NormalizePath(motion), 66, true));
        }
        C3ModelData model = C3ModelData.Create(id, parts, clips, catalog.VirtualMeshNames, files, characterSource: new C3CharacterSource(packedAppearance, 0, selectedBody, 0));
        for (int part = 0; part < parts.Count; part++)
        {
            _ = model.DecodePart(part, clips.Count == 0 ? -1 : 0);
        }
        return model;
    }

    public static C3ModelData File(Guid id, string path, byte[] data)
    {
        string name = C3ModelData.NormalizePath(path);
        C3ResolvedPart part = new("mesh", name, "", "", 0, 0, 0, 0);
        C3ModelData model = C3ModelData.Create(id, [part], [], [], new Dictionary<string, byte[]> { [name] = data });
        _ = model.DecodePart(0, -1);
        return model;
    }
}
