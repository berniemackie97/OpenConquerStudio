using System.Collections.Immutable;
using System.Numerics;

namespace OpenConquer.Content.Mesh;

public sealed record C3ModelClip(string Name, string MotionPath, int FrameMilliseconds, bool Repeat);
public sealed record C3ModelSource(string Path, int Length, string Sha256);
public sealed record C3ModelDefinition(int SchemaVersion, Guid Id, ImmutableArray<C3ResolvedPart> Parts,
    ImmutableArray<C3ModelClip> Clips, ImmutableArray<string> VirtualMeshNames, ImmutableArray<C3ModelSource> Sources);
public sealed record C3ModelTrack(C3PhyMesh Mesh, C3Motion? Motion, bool IsAttachment);

/// <summary>A self-contained compatibility asset. Original bytes retain fields not yet decoded or rendered.</summary>
public sealed class C3ModelData
{
    public const int MaximumPayloadBytes = 64 * 1024 * 1024;
    private readonly Dictionary<string, ReadOnlyMemory<byte>> _files;

    internal C3ModelData(C3ModelDefinition definition, Dictionary<string, ReadOnlyMemory<byte>> files)
    {
        Definition = definition;
        _files = files;
        Validate();
    }

    public C3ModelDefinition Definition
    {
        get;
    }
    public int PayloadBytes => _files.Values.Sum(f => f.Length);

    public ReadOnlyMemory<byte> Source(string path) => _files.TryGetValue(NormalizePath(path), out ReadOnlyMemory<byte> bytes)
        ? bytes : throw new InvalidDataException($"Model dependency '{path}' is missing.");

    public static C3ModelData Create(Guid id, IEnumerable<C3ResolvedPart> parts, IEnumerable<C3ModelClip> clips,
        IEnumerable<string> virtualMeshNames, IReadOnlyDictionary<string, byte[]> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        Dictionary<string, ReadOnlyMemory<byte>> owned = new(StringComparer.Ordinal);
        long total = 0;
        foreach ((string path, byte[] bytes) in files)
        {
            total += bytes.Length;
            if (total > MaximumPayloadBytes || owned.Count >= 128)
            {
                throw new InvalidDataException("Model source data exceeds 128 files or 64 MiB.");
            }
            owned.Add(NormalizePath(path), bytes.ToArray());
        }
        ImmutableArray<C3ModelSource> sources = [.. owned.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => new C3ModelSource(p.Key, p.Value.Length, C3ModelCodec.Hash(p.Value.Span)))];
        return new C3ModelData(new C3ModelDefinition(1, id, [.. parts], [.. clips],
            [.. virtualMeshNames.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal)], sources), owned);
    }

    public C3ModelData WithPlayback(int clipIndex, int frameMilliseconds, bool repeat)
    {
        if (clipIndex < 0 || clipIndex >= Definition.Clips.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(clipIndex));
        }
        C3ModelClip clip = Definition.Clips[clipIndex] with
        {
            FrameMilliseconds = frameMilliseconds,
            Repeat = repeat
        };
        return new C3ModelData(Definition with
        {
            Clips = Definition.Clips.SetItem(clipIndex, clip)
        }, _files);
    }

    public ImmutableArray<C3ModelTrack> DecodePart(int partIndex, int clipIndex)
    {
        C3ResolvedPart part = Definition.Parts[partIndex];
        C3MeshFile geometry = C3MeshFile.Parse(Source(part.MeshPath));
        C3MeshFile motionFile = clipIndex < 0 ? geometry : C3MeshFile.Parse(Source(Definition.Clips[clipIndex].MotionPath));
        C3MeshSection[] meshes = [.. geometry.Sections.Where(s => (s.Identifier & 0x00FFFFFF) == 0x00594850)];
        C3MeshSection[] motions = [.. motionFile.Sections.Where(s => s.Identifier == 0x49544F4D)];
        if (meshes.Length == 0 || meshes.Length > 16 || motions.Length != meshes.Length)
        {
            throw new InvalidDataException("Model geometry and motion require matching ordinal track counts (1–16).");
        }
        return [.. meshes.Select((section, i) =>
        {
            C3PhyMesh mesh = C3PhyMeshReader.Read(section);
            return new C3ModelTrack(mesh, C3MotionReader.Read(motions[i]),
                Definition.VirtualMeshNames.Contains(mesh.Name, StringComparer.OrdinalIgnoreCase));
        })];
    }

    private void Validate()
    {
        if (Definition.SchemaVersion != 1 || Definition.Id == Guid.Empty || Definition.Parts.IsDefaultOrEmpty ||
            Definition.Parts.Length > 4 || Definition.Clips.IsDefault || Definition.Clips.Length > 64 ||
            Definition.VirtualMeshNames.IsDefault || Definition.VirtualMeshNames.Length > 256 ||
            Definition.Sources.IsDefaultOrEmpty || Definition.Sources.Length > 128 || PayloadBytes > MaximumPayloadBytes)
        {
            throw new InvalidDataException("Invalid model version, identity or resource budgets.");
        }
        foreach (string name in Definition.VirtualMeshNames)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 256)
            {
                throw new InvalidDataException("Invalid virtual mesh identifier.");
            }
        }
        foreach (C3ResolvedPart part in Definition.Parts)
        {
            if (part is { MeshPath: null } || string.IsNullOrWhiteSpace(part.Name))
            {
                throw new InvalidDataException("Invalid model part.");
            }
            _ = Source(NormalizePath(part.MeshPath));
            if (!string.IsNullOrEmpty(part.TexturePath))
            {
                _ = Source(NormalizePath(part.TexturePath));
            }
            if (part.SecondaryTexturePath is not null)
            {
                _ = Source(NormalizePath(part.SecondaryTexturePath));
            }
        }
        foreach (C3ModelClip clip in Definition.Clips)
        {
            if (clip is null || string.IsNullOrWhiteSpace(clip.Name) || clip.Name.Length > 128 || clip.FrameMilliseconds is < 1 or > 60000)
            {
                throw new InvalidDataException("Invalid model clip name or timing.");
            }
            _ = Source(NormalizePath(clip.MotionPath));
        }
        long matrices = 0;
        long vertices = 0;
        foreach ((string path, ReadOnlyMemory<byte> bytes) in _files)
        {
            if (!path.EndsWith(".c3", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            C3MeshFile file = C3MeshFile.Parse(bytes);
            foreach (C3MeshSection section in file.Sections)
            {
                if ((section.Identifier & 0x00FFFFFF) == 0x00594850)
                {
                    C3PhyMesh mesh = C3PhyMeshReader.Read(section);
                    vertices += mesh.Vertices.Count;
                    if (vertices > 1_000_000)
                    {
                        throw new InvalidDataException("Model geometry exceeds its decoded vertex budget.");
                    }
                }
                else if (section.Identifier == 0x49544F4D)
                {
                    C3Motion motion = C3MotionReader.Read(section);
                    matrices += (long)motion.BoneCount * motion.KeyframeCount;
                    if (matrices > 1_000_000 || motion.FrameCount > int.MaxValue || motion.KeyframeCount == 0)
                    {
                        throw new InvalidDataException("Model motion exceeds its decoded matrix budget or has no sampleable keys.");
                    }
                    uint previous = 0;
                    for (int key = 0; key < motion.KeyframeCount; key++)
                    {
                        uint index = motion.GetKeyframeIndex(key);
                        if (index > int.MaxValue || key > 0 && index <= previous)
                        {
                            throw new InvalidDataException("Motion keys must be strictly ordered signed frame positions.");
                        }
                        previous = index;
                        for (int bone = 0; bone < motion.BoneCount; bone++)
                        {
                            Matrix4x4 matrix = motion.GetBoneMatrix(key, bone);
                            ReadOnlySpan<float> values = System.Runtime.InteropServices.MemoryMarshal.Cast<Matrix4x4, float>(new ReadOnlySpan<Matrix4x4>(in matrix));
                            foreach (float value in values)
                            {
                                if (!float.IsFinite(value))
                                {
                                    throw new InvalidDataException("Motion matrices must be finite.");
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    internal static string NormalizePath(string path) =>
        ClientContentPath.NormalizeVirtualPath(path, nameof(path), 512).ToLowerInvariant();
}
