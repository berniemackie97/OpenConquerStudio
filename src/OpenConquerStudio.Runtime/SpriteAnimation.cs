using System.Collections.Immutable;

namespace OpenConquerStudio.Runtime;

public sealed record AnimationFrame(Guid Id, Guid AtlasId, Guid SpriteId, int DurationTicks);

/// <summary>An elapsed-time sprite clip. Time is authored independently of rendering frequency.</summary>
public sealed class SpriteAnimation
{
    public const int MaximumFrames = 4096;

    public SpriteAnimation(Guid id, int ticksPerSecond, bool repeat, ImmutableArray<AnimationFrame> frames)
    {
        if (id == Guid.Empty || ticksPerSecond is < 1 or > 1_000_000 || frames.IsDefaultOrEmpty || frames.Length > MaximumFrames)
        {
            throw new InvalidDataException("A sprite animation requires a valid identity, timebase, and 1–4096 frames.");
        }

        HashSet<Guid> ids = [];
        long duration = 0;
        foreach (AnimationFrame frame in frames)
        {
            if (frame is null || frame.Id == Guid.Empty || !ids.Add(frame.Id) || frame.AtlasId == Guid.Empty ||
                frame.SpriteId == Guid.Empty || frame.DurationTicks <= 0)
            {
                throw new InvalidDataException("Animation frame identities, references, and durations must be valid.");
            }

            duration = checked(duration + frame.DurationTicks);
        }

        Id = id;
        TicksPerSecond = ticksPerSecond;
        Repeat = repeat;
        Frames = frames;
        DurationTicks = duration;
    }

    public Guid Id
    {
        get;
    }
    public int TicksPerSecond
    {
        get;
    }
    public bool Repeat
    {
        get;
    }
    public ImmutableArray<AnimationFrame> Frames
    {
        get;
    }
    public long DurationTicks
    {
        get;
    }

    public int Sample(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        long ticks = (long)((Int128)elapsed.Ticks * TicksPerSecond / TimeSpan.TicksPerSecond);
        return SampleTicks(ticks);
    }

    public int SampleTicks(long elapsedTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elapsedTicks);
        long cursor = Repeat ? elapsedTicks % DurationTicks : Math.Min(elapsedTicks, DurationTicks - 1);
        for (int index = 0; index < Frames.Length; index++)
        {
            if (cursor < Frames[index].DurationTicks)
            {
                return index;
            }

            cursor -= Frames[index].DurationTicks;
        }

        throw new InvalidOperationException("Animation duration does not match its validated frames.");
    }
}
