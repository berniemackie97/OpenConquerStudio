namespace OpenConquer.Rendering.World;

/// <summary>Per-puzzle bitmap 5517 frame counter; advances once after a render when its interval elapsed.</summary>
internal sealed class WorldPuzzleAnimationClock
{
    private readonly uint _intervalMilliseconds;
    private uint _lastRenderMilliseconds;
    private bool _initialized;
    private uint _frame;

    public WorldPuzzleAnimationClock(uint intervalMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfZero(intervalMilliseconds);
        _intervalMilliseconds = intervalMilliseconds;
    }

    public uint FrameForRender(uint nowMilliseconds)
    {
        if (!_initialized)
        {
            _lastRenderMilliseconds = nowMilliseconds;
            _initialized = true;
        }

        uint frame = _frame;
        if (unchecked(nowMilliseconds - _lastRenderMilliseconds) >= _intervalMilliseconds)
        {
            _frame++;
            _lastRenderMilliseconds = nowMilliseconds;
        }

        return frame;
    }
}
