using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Mesh;

public sealed record C3CharacterSource(uint Appearance, uint HairStyle, uint BodyModelId, uint ArmetModelId);
public sealed record C3ModelMotionBinding(int PartIndex, string MotionPath);
public sealed record C3ModelPartBinding(int PartIndex, int ParentPartIndex, string ParentSubmesh, ImmutableArray<float> LocalMatrix);
public sealed record C3ModelClip(string Name, string MotionPath, int FrameMilliseconds, bool Repeat)
{
    public ImmutableArray<C3ModelMotionBinding> PartMotions { get; init; } = [];
}
public sealed record C3ModelSource(string Path, int Length, string Sha256);
public sealed record C3ModelDefinition(int SchemaVersion, Guid Id, ImmutableArray<C3ResolvedPart> Parts,
    ImmutableArray<C3ModelClip> Clips, ImmutableArray<string> VirtualMeshNames, ImmutableArray<C3ModelSource> Sources)
{
    public ImmutableArray<C3ModelPartBinding> Bindings { get; init; } = [];
    public C3CharacterSource? CharacterSource
    {
        get; init;
    }
}
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
        IEnumerable<string> virtualMeshNames, IReadOnlyDictionary<string, byte[]> files, IEnumerable<C3ModelPartBinding>? bindings = null, C3CharacterSource? characterSource = null)
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
        return new C3ModelData(new C3ModelDefinition(2, id, [.. parts], [.. clips],
            [.. virtualMeshNames.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal)], sources)
        {
            Bindings = bindings is null ? [] : [.. bindings],
            CharacterSource = characterSource
        }, owned);
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
        C3ModelClip? clip = clipIndex < 0 ? null : Definition.Clips[clipIndex];
        string? motionPath = clip?.PartMotions.FirstOrDefault(b => b.PartIndex == partIndex)?.MotionPath ?? clip?.MotionPath;
        C3MeshFile motionFile = motionPath is null ? geometry : C3MeshFile.Parse(Source(motionPath));
        C3MeshSection[] meshes = [.. geometry.Sections.Where(s => (s.Identifier & 0x00FFFFFF) == 0x00594850)];
        C3MeshSection[] motions = [.. motionFile.Sections.Where(s => s.Identifier == 0x49544F4D)];
        if (meshes.Length == 0 || meshes.Length > 16 || motions.Length < meshes.Length || clipIndex < 0 && motions.Length != meshes.Length)
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
        if (Definition.SchemaVersion is not (1 or 2) || Definition.Id == Guid.Empty || Definition.Parts.IsDefaultOrEmpty ||
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
        long textureBytes = 0;
        foreach (string path in Definition.Parts.SelectMany(p => new[] { p.TexturePath, p.SecondaryTexturePath })
            .OfType<string>().Where(p => p.Length > 0).Select(NormalizePath).Distinct(StringComparer.Ordinal))
        {
            RgbaImage image = path.EndsWith(".tga", StringComparison.OrdinalIgnoreCase)
                ? TargaImageReader.DecodeRle32Bit(Source(path).Span) : DdsImageReader.Decode(Source(path).Span);
            textureBytes += image.Pixels.Length;
            if (textureBytes > 64 * 1024 * 1024)
            {
                throw new InvalidDataException("Model texture dependencies exceed the decoded pixel budget.");
            }
        }
        ValidateBindings();
        foreach (C3ModelClip clip in Definition.Clips)
        {
            if (clip is null || string.IsNullOrWhiteSpace(clip.Name) || clip.Name.Length > 128 || clip.FrameMilliseconds is < 1 or > 60000)
            {
                throw new InvalidDataException("Invalid model clip name or timing.");
            }
            _ = Source(NormalizePath(clip.MotionPath));
            if (clip.PartMotions.IsDefault || clip.PartMotions.Any(b => b is null) || clip.PartMotions.Length > Definition.Parts.Length ||
                clip.PartMotions.Select(b => b.PartIndex).Distinct().Count() != clip.PartMotions.Length)
            {
                throw new InvalidDataException("Invalid per-part motion bindings.");
            }
            foreach (C3ModelMotionBinding binding in clip.PartMotions)
            {
                if (binding.PartIndex < 0 || binding.PartIndex >= Definition.Parts.Length)
                {
                    throw new InvalidDataException("Motion binding refers to a missing part.");
                }
                _ = Source(NormalizePath(binding.MotionPath));
            }
        }
        Dictionary<string, (int Geometry, int Motion, Dictionary<string, int> Names)> shapes = new(StringComparer.Ordinal);
        long matrices = 0;
        long keyframes = 0;
        long vertices = 0;
        foreach ((string path, ReadOnlyMemory<byte> bytes) in _files)
        {
            if (!path.EndsWith(".c3", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            C3MeshFile file = C3MeshFile.Parse(bytes);
            int geometryCount = 0;
            int motionCount = 0;
            Dictionary<string, int> names = new(StringComparer.OrdinalIgnoreCase);
            foreach (C3MeshSection section in file.Sections)
            {
                if ((section.Identifier & 0x00FFFFFF) == 0x00594850)
                {
                    C3PhyMesh mesh = C3PhyMeshReader.Read(section);
                    geometryCount++;
                    names[mesh.Name] = names.GetValueOrDefault(mesh.Name) + 1;
                    foreach (C3PhyVertex vertex in mesh.Vertices)
                    {
                        if (!float.IsFinite(vertex.Normal.X) || !float.IsFinite(vertex.Normal.Y) || !float.IsFinite(vertex.Normal.Z) ||
                            !float.IsFinite(vertex.TextureCoordinate.X) || !float.IsFinite(vertex.TextureCoordinate.Y) ||
                            !float.IsFinite(vertex.BoneWeight1) || !float.IsFinite(vertex.BoneWeight2))
                        {
                            throw new InvalidDataException("Model vertex attributes must be finite.");
                        }
                    }
                    vertices += mesh.Vertices.Count;
                    if (vertices > 1_000_000)
                    {
                        throw new InvalidDataException("Model geometry exceeds its decoded vertex budget.");
                    }
                }
                else if (section.Identifier == 0x49544F4D)
                {
                    if (++motionCount > 16)
                    {
                        throw new InvalidDataException("Model exceeds 16 ordinal motion tracks per source.");
                    }
                    ReadOnlySpan<byte> motionBytes = section.Payload.Span;
                    if (motionBytes.Length < 12)
                    {
                        throw new InvalidDataException("Truncated motion header.");
                    }
                    uint bones = BinaryPrimitives.ReadUInt32LittleEndian(motionBytes);
                    uint encoding = BinaryPrimitives.ReadUInt32LittleEndian(motionBytes[8..]);
                    bool keyed = encoding is 0x59454B4B or 0x59454B5A or 0x59454B58;
                    if (keyed && motionBytes.Length < 16)
                    {
                        throw new InvalidDataException("Truncated keyed motion header.");
                    }
                    uint keys = BinaryPrimitives.ReadUInt32LittleEndian(motionBytes[(keyed ? 12 : 4)..]);
                    keyframes += keys;
                    if (keyframes > 1_000_000 || bones > 1000 || (long)keys * bones > 1_000_000 - matrices)
                    {
                        throw new InvalidDataException("Motion exceeds its decoded allocation budget.");
                    }
                    matrices += (long)keys * bones;
                    C3Motion motion = C3MotionReader.Read(section);
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
            shapes.Add(path, (geometryCount, motionCount, names));
        }
        for (int partIndex = 0; partIndex < Definition.Parts.Length; partIndex++)
        {
            var geometry = shapes.GetValueOrDefault(NormalizePath(Definition.Parts[partIndex].MeshPath));
            if (geometry.Geometry < 1 || geometry.Geometry != geometry.Motion)
            {
                throw new InvalidDataException("Part geometry must include matching embedded motion tracks.");
            }
            foreach (C3ModelClip clip in Definition.Clips)
            {
                string path = clip.PartMotions.FirstOrDefault(b => b.PartIndex == partIndex)?.MotionPath ?? clip.MotionPath;
                if (shapes.GetValueOrDefault(NormalizePath(path)).Motion < geometry.Geometry)
                {
                    throw new InvalidDataException("Clip has fewer motion tracks than its geometry.");
                }
            }
        }
        foreach (C3ModelPartBinding binding in Definition.Bindings.Where(b => b.ParentPartIndex >= 0))
        {
            var parent = shapes[NormalizePath(Definition.Parts[binding.ParentPartIndex].MeshPath)];
            if (parent.Names.GetValueOrDefault(binding.ParentSubmesh) != 1)
            {
                throw new InvalidDataException("Attachment submesh is absent or ambiguous.");
            }
        }
    }

    private void ValidateBindings()
    {
        if (Definition.Bindings.IsDefault || Definition.Bindings.Length > Definition.Parts.Length ||
            Definition.Bindings.Any(b => b is null) ||
            Definition.Bindings.Select(b => b.PartIndex).Distinct().Count() != Definition.Bindings.Length)
        {
            throw new InvalidDataException("Part bindings must have unique bounded identities.");
        }
        foreach (C3ModelPartBinding binding in Definition.Bindings)
        {
            if (binding.PartIndex < 0 || binding.PartIndex >= Definition.Parts.Length || binding.ParentPartIndex < -1 ||
                binding.ParentPartIndex >= Definition.Parts.Length || binding.LocalMatrix.IsDefault ||
                binding.LocalMatrix.Length is not (0 or 16) || binding.LocalMatrix.Any(v => !float.IsFinite(v)) ||
                binding.ParentSubmesh is null || binding.ParentSubmesh.Length > 256)
            {
                throw new InvalidDataException("Part binding has an invalid index, matrix or attachment identifier.");
            }
            HashSet<int> visited = [binding.PartIndex];
            C3ModelPartBinding? current = binding;
            while (current is { ParentPartIndex: >= 0 })
            {
                if (!visited.Add(current.ParentPartIndex) || string.IsNullOrWhiteSpace(current.ParentSubmesh))
                {
                    throw new InvalidDataException("Part links must resolve without cycles.");
                }
                current = Definition.Bindings.FirstOrDefault(b => b.PartIndex == current.ParentPartIndex);
            }
        }
    }

    public Matrix4x4 PartLocalMatrix(int partIndex)
    {
        C3ModelPartBinding? binding = Definition.Bindings.FirstOrDefault(b => b.PartIndex == partIndex);
        if (binding is null || binding.LocalMatrix.IsEmpty)
        {
            return Matrix4x4.Identity;
        }
        return System.Runtime.InteropServices.MemoryMarshal.Read<Matrix4x4>(
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(binding.LocalMatrix.AsSpan()));
    }

    internal static string NormalizePath(string path) =>
        ClientContentPath.NormalizeVirtualPath(path, nameof(path), 512).ToLowerInvariant();
}
