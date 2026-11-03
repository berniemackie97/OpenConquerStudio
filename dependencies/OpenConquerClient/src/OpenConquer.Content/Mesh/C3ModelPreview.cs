using System.Collections.Immutable;
using System.Numerics;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Mesh;

public sealed record C3ModelPreviewPart(RgbaImage? Texture, ImmutableArray<C3ModelTrack> Tracks);

/// <summary>CPU-decoded preview snapshot. Construct off the graphics thread, then retain across context loss.</summary>
public sealed class C3ModelPreview
{
    private C3ModelPreview(C3ModelData model, int clipIndex, ImmutableArray<C3ModelPreviewPart> parts)
    {
        Model = model;
        ClipIndex = clipIndex;
        Parts = parts;
        Vector3 minimum = new(float.PositiveInfinity);
        Vector3 maximum = new(float.NegativeInfinity);
        for (int partIndex = 0; partIndex < parts.Length; partIndex++)
        {
            Matrix4x4 transform = PartTransform(partIndex, 0);
            foreach (C3ModelTrack track in parts[partIndex].Tracks)
            {
                if (track.IsAttachment)
                {
                    continue;
                }
                Matrix4x4[] bones = new Matrix4x4[track.Motion?.BoneCount ?? 0];
                C3PhyOutputVertex[] vertices = new C3PhyOutputVertex[track.Mesh.Vertices.Count];
                C3PhySkinner.Calculate(track.Mesh, track.Motion, 0, null, 0, 0, bones, vertices);
                foreach (C3PhyOutputVertex vertex in vertices)
                {
                    Vector3 point = Vector3.Transform(vertex.Position, transform);
                    minimum = Vector3.Min(minimum, point);
                    maximum = Vector3.Max(maximum, point);
                }
            }
        }
        if (!float.IsFinite(minimum.X) || !float.IsFinite(maximum.X))
        {
            throw new InvalidDataException("Model has no finite drawable geometry.");
        }
        BoundsMin = minimum;
        BoundsMax = maximum;
        // The primary clip owns the clock. A longer embedded attachment motion must not extend body idle playback.
        C3Motion? primary = Clip is null ? parts[0].Tracks[0].Motion : C3MotionReader.Read(
            C3MeshFile.Parse(model.Source(Clip.MotionPath)).Sections.First(s => s.Identifier == 0x49544F4D));
        FrameCount = Math.Max(1, primary?.FrameCount ?? 0);
    }

    public C3ModelData Model
    {
        get;
    }
    public int ClipIndex
    {
        get;
    }
    public ImmutableArray<C3ModelPreviewPart> Parts
    {
        get;
    }
    public Vector3 BoundsMin
    {
        get;
    }
    public Vector3 BoundsMax
    {
        get;
    }
    public uint FrameCount
    {
        get;
    }
    public C3ModelClip? Clip => ClipIndex < 0 ? null : Model.Definition.Clips[ClipIndex];

    /// <summary>Native draw-chain accumulation; the separate transform-query target leg is not part of this path.</summary>
    public Matrix4x4 PartTransform(int partIndex, int frame)
    {
        Matrix4x4 world = Matrix4x4.Identity;
        C3ModelPartBinding? binding = Model.Definition.Bindings.FirstOrDefault(b => b.PartIndex == partIndex);
        while (binding is { ParentPartIndex: >= 0 })
        {
            C3ModelTrack[] anchors = [.. Parts[binding.ParentPartIndex].Tracks.Where(t =>
                string.Equals(t.Mesh.Name, binding.ParentSubmesh, StringComparison.OrdinalIgnoreCase))];
            if (anchors.Length != 1 || !C3MotionSampler.TrySampleBone(anchors[0].Motion, 0, frame, out Matrix4x4 bone))
            {
                throw new InvalidDataException("An attachment anchor is missing, ambiguous or unsampleable.");
            }
            Vector4 translation = Vector4.Transform(new Vector4(bone.M41, bone.M42, bone.M43, 1), Model.PartLocalMatrix(binding.ParentPartIndex));
            if (MathF.Abs(translation.W - 1f) > 1.1920929e-7f)
            {
                translation /= translation.W;
            }
            if (!float.IsFinite(translation.X) || !float.IsFinite(translation.Y) || !float.IsFinite(translation.Z))
            {
                throw new InvalidDataException("Attachment translation is not finite.");
            }
            bone.M41 = translation.X;
            bone.M42 = translation.Y;
            bone.M43 = translation.Z;
            world = bone * world;
            binding = Model.Definition.Bindings.FirstOrDefault(b => b.PartIndex == binding.ParentPartIndex);
        }
        return Model.PartLocalMatrix(partIndex) * world;
    }

    public static C3ModelPreview Create(C3ModelData model, int clipIndex)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (clipIndex < -1 || clipIndex >= model.Definition.Clips.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(clipIndex));
        }
        List<C3ModelPreviewPart> parts = [];
        long pixels = 0;
        for (int i = 0; i < model.Definition.Parts.Length; i++)
        {
            string path = model.Definition.Parts[i].TexturePath;
            RgbaImage? texture = string.IsNullOrEmpty(path) ? null : path.EndsWith(".tga", StringComparison.OrdinalIgnoreCase)
                ? TargaImageReader.DecodeRle32Bit(model.Source(path).Span) : DdsImageReader.Decode(model.Source(path).Span);
            pixels += texture?.Pixels.Length ?? 0;
            if (pixels > 64 * 1024 * 1024)
            {
                throw new InvalidDataException("Model preview textures exceed 64 MiB.");
            }
            parts.Add(new C3ModelPreviewPart(texture, model.DecodePart(i, clipIndex)));
        }
        return new C3ModelPreview(model, clipIndex, [.. parts]);
    }
}
