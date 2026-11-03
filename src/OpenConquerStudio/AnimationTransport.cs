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
            if (_animation is null)
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
            if (e.Property == Slider.ValueProperty && _animation is not null)
            {
                long position = (long)_position.Value;
                if (!_updating)
                {
                    _startTicks = position;
                    _clock.Restart();
                }

                int frame = _animation.SampleTicks(position);
                _label.Text = $"Frame {frame + 1}/{_animation.Frames.Length} · {position}/{_animation.DurationTicks} ticks";
                select(frame);
            }
        };
        _timer.Tick += (_, _) =>
        {
            if (_animation is null)
            {
                return;
            }

            long elapsed = _startTicks + (long)((Int128)_clock.Elapsed.Ticks * _animation.TicksPerSecond / TimeSpan.TicksPerSecond);
            _updating = true;
            try
            {
                _position.Value = _animation.Repeat ? elapsed % _animation.DurationTicks : Math.Min(elapsed, _animation.DurationTicks - 1);
                if (!_animation.Repeat && elapsed >= _animation.DurationTicks)
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
        IsVisible = animation is not null;
        _position.Maximum = Math.Max(1, (animation?.DurationTicks ?? 1) - 1);
        _position.Value = 0;
        _label.Text = animation is null ? "" : $"Frame 1/{animation.Frames.Length} · 0/{animation.DurationTicks} ticks";
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
