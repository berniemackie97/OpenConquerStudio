using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio;

internal sealed class AnimationTransport : StackPanel
{
    private readonly Button _play = new() { Content = "Play", Name = "PlayAnimation" };
    private readonly Slider _position = new() { Minimum = 0, Maximum = 1, Name = "AnimationPosition" };
    private readonly TextBlock _label = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch _clock = new();
    private SpriteAnimation? _animation;
    private long _startTicks;
    private bool _updating;
    private int _modelFrameCount;
    private int _modelFrameMilliseconds;
    private bool _modelRepeat;
    private long DurationTicks => _animation?.DurationTicks ?? (long)_modelFrameCount * _modelFrameMilliseconds;
    private int TicksPerSecond => _animation?.TicksPerSecond ?? 1000;
    private int FrameCount => _animation?.Frames.Length ?? _modelFrameCount;
    private bool Repeat => _animation?.Repeat ?? _modelRepeat;
    private int Sample(long position) => _animation?.SampleTicks(position) ?? (int)(position / _modelFrameMilliseconds);

    public AnimationTransport(Action<int> select)
    {
        Spacing = 6;
        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        buttons.Children.Add(_play);
        Button stop = new()
        {
            Content = "Stop",
            Name = "StopAnimation"
        };
        buttons.Children.Add(stop);
        buttons.Children.Add(_label);
        Children.Add(buttons);
        Children.Add(_position);
        _play.Click += (_, _) =>
        {
            if (DurationTicks == 0)
            {
                return;
            }

            if (_timer.IsEnabled)
            {
                Pause();
            }
            else
            {
                _startTicks = (long)_position.Value;
                _clock.Restart();
                _timer.Start();
                _play.Content = "Pause";
            }
        };
        stop.Click += (_, _) => { Pause(); _position.Value = 0; };
        _position.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty && DurationTicks > 0)
            {
                long position = (long)_position.Value;
                if (!_updating)
                {
                    _startTicks = position;
                    _clock.Restart();
                }

                int frame = Sample(position);
                _label.Text = $"Frame {frame + 1}/{FrameCount} · {position}/{DurationTicks} ticks";
                select(frame);
            }
        };
        _timer.Tick += (_, _) =>
        {
            if (DurationTicks == 0)
            {
                return;
            }

            long elapsed = _startTicks + (long)((Int128)_clock.Elapsed.Ticks * TicksPerSecond / TimeSpan.TicksPerSecond);
            _updating = true;
            try
            {
                _position.Value = Repeat ? elapsed % DurationTicks : Math.Min(elapsed, DurationTicks - 1);
                if (!Repeat && elapsed >= DurationTicks)
                {
                    Pause();
                }
            }
            finally
            {
                _updating = false;
            }
        };
        DetachedFromVisualTree += (_, _) => Pause();
        IsVisible = false;
    }

    public void Load(SpriteAnimation? animation)
    {
        Pause();
        _animation = animation;
        _modelFrameCount = 0;
        _play.IsEnabled = (animation?.Frames.Length ?? 0) > 1;
        IsVisible = animation is not null;
        _position.Maximum = Math.Max(1, (animation?.DurationTicks ?? 1) - 1);
        _position.Value = 0;
        _label.Text = animation is null ? "" : $"Frame 1/{animation.Frames.Length} · 0/{animation.DurationTicks} ticks";
    }

    public void LoadModel(int frameCount, int frameMilliseconds, bool repeat)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameMilliseconds);
        Pause();
        _animation = null;
        _modelFrameCount = frameCount;
        _modelFrameMilliseconds = frameMilliseconds;
        _modelRepeat = repeat;
        IsVisible = true;
        _play.IsEnabled = frameCount > 1;
        _position.Maximum = DurationTicks - 1;
        _position.Value = 0;
        _label.Text = $"Frame 1/{FrameCount} · 0/{DurationTicks} milliseconds";
    }

    public void SelectFrame(int index)
    {
        if (_animation is not null)
        {
            Pause();
            _position.Value = _animation.Frames.Take(index).Sum(f => (long)f.DurationTicks);
        }
    }

    public void Pause()
    {
        _timer.Stop();
        _clock.Stop();
        _play.Content = "Play";
    }
}
