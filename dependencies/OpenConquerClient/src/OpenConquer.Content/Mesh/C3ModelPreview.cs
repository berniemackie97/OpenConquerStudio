using System.Collections.Immutable;
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
        FrameCount = Math.Max(1, parts.SelectMany(p => p.Tracks).Max(t => t.Motion?.FrameCount ?? 0));
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
    public uint FrameCount
    {
        get;
    }
    public C3ModelClip? Clip => ClipIndex < 0 ? null : Model.Definition.Clips[ClipIndex];

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
