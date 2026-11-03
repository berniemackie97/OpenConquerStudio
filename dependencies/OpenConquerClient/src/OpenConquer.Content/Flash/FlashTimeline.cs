namespace OpenConquer.Content.Flash;

/// <summary>
/// One object on a timeline frame, as the SWF display list holds it after that frame's tags.
/// <see cref="PlacedFrame"/> is the frame whose PlaceObject created the instance: an instance survives from frame to
/// frame, and across a seek, while its depth keeps the same character and placed frame.
/// </summary>
public sealed record FlashPlacement(int Depth, int CharacterId, string? Name, FlashMatrix Matrix, FlashColorTransform ColorTransform,
    int? ClipDepth, int? Ratio, int PlacedFrame);

/// <summary>A static timeline: the display list of every frame (index 0 is frame 1) and the frame labels.</summary>
public sealed class FlashTimeline
{
    public FlashTimeline(IReadOnlyList<IReadOnlyList<FlashPlacement>> frames, IReadOnlyDictionary<string, int> labels)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(labels);

        if (frames.Count == 0)
        {
            throw new ArgumentException("A timeline has at least one frame.", nameof(frames));
        }

        Frames = frames;
        Labels = labels;
    }

    public IReadOnlyList<IReadOnlyList<FlashPlacement>> Frames
    {
        get;
    }

    public IReadOnlyDictionary<string, int> Labels
    {
        get;
    }

    public int FrameCount => Frames.Count;

    /// <summary>The display list after frame <paramref name="frame"/> (1-based).</summary>
    public IReadOnlyList<FlashPlacement> DisplayList(int frame)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(frame, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(frame, Frames.Count);

        return Frames[frame - 1];
    }
}
