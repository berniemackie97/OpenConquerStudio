using System.Collections.Immutable;
using OpenConquer.Content;
using OpenConquer.Content.Mesh;

namespace OpenConquerStudio.ConquerImport;

/// <summary>Imports catalog-backed body and armet assets using the WORLD-021/022 selection and native attachment contracts.</summary>
public static class CharacterImporter
{
    public static C3ModelData Read(IClientContentSource source, uint appearance, uint hairStyle = 0, CancellationToken token = default) =>
        Read(source, appearance, hairStyle, 0, 0, token);

    public static C3ModelData Read(IClientContentSource source, uint appearance, uint hairStyle, uint bodyModelId, uint armetModelId, CancellationToken token = default)
    {
        C3ModelData body = C3ModelImporter.Character(source, Guid.NewGuid(), appearance, bodyModelId, token);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(hairStyle, 999u);
        if (hairStyle % 100 == 0 && armetModelId == 0)
        {
            return body;
        }
        C3RoleCatalog catalog = C3RoleCatalog.Load(source);
        uint hairId = armetModelId == 0 ? checked(appearance % 1000 * 1_000_000 + 0x1D0D8 + hairStyle) : armetModelId;
        if (!catalog.TryResolve("armet", hairId, out IReadOnlyList<C3ResolvedPart> headwear) || headwear.Count != 1 || body.Definition.Parts.Length != 1)
        {
            throw new InvalidDataException("The selected hair model or body attachment layout is unavailable.");
        }
        Dictionary<string, byte[]> files = body.Definition.Sources.ToDictionary(f => f.Path, f => body.Source(f.Path).ToArray(), StringComparer.Ordinal);
        C3ResolvedPart part = headwear[0];
        Capture(part.MeshPath);
        Capture(part.TexturePath);
        if (part.SecondaryTextureId != 0 && part.SecondaryTexturePath is null)
        {
            throw new InvalidDataException("Headwear has an unresolved secondary texture dependency.");
        }
        if (part.SecondaryTexturePath is not null)
        {
            Capture(part.SecondaryTexturePath);
        }
        ImmutableArray<C3ModelClip> clips = [.. body.Definition.Clips.Select(clip => clip with
        {
            PartMotions = [new C3ModelMotionBinding(1, part.MeshPath)]
        })];
        C3CharacterSource origin = body.Definition.CharacterSource ?? throw new InvalidDataException("Imported body is missing its catalog source association.");
        C3ModelData model = C3ModelData.Create(body.Definition.Id, [.. body.Definition.Parts, part], clips,
            body.Definition.VirtualMeshNames, files, [new C3ModelPartBinding(1, 0, "v_armet", [])],
            new C3CharacterSource(appearance, hairStyle, origin.BodyModelId, hairId));
        _ = C3ModelPreview.Create(model, clips.IsEmpty ? -1 : 0);
        return model;

        void Capture(string path)
        {
            token.ThrowIfCancellationRequested();
            string normalized = path.Replace('\\', '/').ToLowerInvariant();
            if (!files.ContainsKey(normalized))
            {
                byte[] bytes = ContentReader.ReadRequiredBytes(source, path, ContentLookupMode.PackageOnly, C3MeshFile.MaximumFileBytes);
                if (files.Values.Sum(b => (long)b.Length) + bytes.Length > C3ModelData.MaximumPayloadBytes)
                {
                    throw new InvalidDataException("Character source closure exceeds 64 MiB.");
                }
                files.Add(normalized, bytes);
            }
        }
    }
}
