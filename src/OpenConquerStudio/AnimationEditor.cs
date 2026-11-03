using System.Collections.Immutable;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio;

internal sealed class AnimationEditor : StackPanel
{
    private readonly TextBox _name = new() { Name = "AnimationName" };
    private readonly NumericUpDown _timebase = new() { Minimum = 1, Maximum = 1_000_000, Increment = 1, FormatString = "0", Name = "AnimationTimebase", ParsingNumberStyle = NumberStyles.Integer };
    private readonly CheckBox _repeat = new() { Content = "Repeat", Name = "AnimationRepeat" };
    private readonly ListBox _frames = new() { Height = 180, Name = "AnimationFrames" };
    private readonly ComboBox _sprite = new() { Name = "FrameSprite", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly NumericUpDown _duration = new() { Minimum = 1, Maximum = int.MaxValue, Increment = 1, FormatString = "0", Name = "FrameDuration", Value = 100, ParsingNumberStyle = NumberStyles.Integer };
    private readonly Dictionary<string, Button> _actions = [];
    private AnimationAsset? _asset;
    private int _editingFrame;
    private bool _loading;
    public event Action? PendingChanged;

    public AnimationEditor(Func<Func<AnimationAsset>, Task> edit, Action<int> selected, Func<Task<bool>> navigate)
    {
        Spacing = 8;
        Children.Add(new TextBlock { Text = "Animation name" });
        Children.Add(_name);
        Children.Add(new TextBlock { Text = "Ticks per second" });
        Children.Add(_timebase);
        Children.Add(_repeat);
        Button settings = new()
        {
            Content = "Apply clip settings",
            Name = "ApplyClipSettings"
        };
        settings.Click += async (_, _) => { if (_asset is not null) { await edit(ReadChanges); } };
        Children.Add(settings);
        Children.Add(_frames);
        Children.Add(new TextBlock { Text = "Frame sprite" });
        Children.Add(_sprite);
        Children.Add(new TextBlock { Text = "Duration (ticks)" });
        Children.Add(_duration);
        foreach (string command in new[] { "Update", "Add", "Remove", "Up", "Down" })
        {
            Button button = new()
            {
                Content = command + " frame",
                Name = command + "Frame"
            };
            _actions.Add(command, button);
            Children.Add(button);
            button.Click += async (_, _) =>
            {
                if (_asset is null)
                {
                    return;
                }
                int index = _editingFrame;
                await edit(() =>
                {
                    AnimationAsset asset = ReadChanges();
                    ImmutableArray<AnimationFrame> frames = asset.Frames;
                    if (command == "Add")
                    {
                        frames = frames.Insert(index + 1, frames[index] with
                        {
                            Id = Guid.NewGuid()
                        });
                        index++;
                    }
                    else if (command == "Remove")
                    {
                        frames = frames.RemoveAt(index);
                        index = Math.Min(index, frames.Length - 1);
                    }
                    else if (command is "Up" or "Down")
                    {
                        int target = command == "Up" ? index - 1 : index + 1;
                        if (target < 0 || target >= frames.Length)
                        {
                            return asset;
                        }
                        frames = frames.SetItem(index, frames[target]).SetItem(target, frames[index]);
                        index = target;
                    }
                    return asset with
                    {
                        Frames = frames
                    };
                });
                _frames.SelectedIndex = Math.Clamp(index, 0, _asset.Frames.Length - 1);
            };
        }

        _frames.SelectionChanged += async (_, _) =>
        {
            if (_loading || _asset is null || _frames.SelectedIndex < 0)
            {
                return;
            }
            int target = _frames.SelectedIndex;
            if (target != _editingFrame && !await navigate())
            {
                _loading = true;
                try
                {
                    _frames.SelectedIndex = _editingFrame;
                }
                finally { _loading = false; }
                return;
            }
            _loading = true;
            try
            {
                _frames.SelectedIndex = target;
            }
            finally { _loading = false; }
            LoadFrame(target);
            selected(target);
        };
        _name.TextChanged += (_, _) => Changed();
        _repeat.IsCheckedChanged += (_, _) => Changed();
        _sprite.SelectionChanged += (_, _) => Changed();
        foreach (NumericUpDown input in new[] { _timebase, _duration })
        {
            input.PropertyChanged += (_, e) => { if (e.Property == NumericUpDown.TextProperty) { Changed(); } };
        }
    }

    private void Changed()
    {
        if (!_loading)
        {
            PendingChanged?.Invoke();
        }
    }
    public int SelectedFrame => _editingFrame;
    public bool HasPending
    {
        get
        {
            if (_asset is null)
            {
                return false;
            }
            try
            {
                AnimationAsset draft = ReadChanges();
                return draft.Name != _asset.Name || draft.TicksPerSecond != _asset.TicksPerSecond || draft.Repeat != _asset.Repeat || !draft.Frames.SequenceEqual(_asset.Frames);
            }
            catch (InvalidDataException) { return true; }
        }
    }
    public AnimationAsset ReadChanges()
    {
        AnimationAsset asset = _asset ?? throw new InvalidOperationException("Select an animation first.");
        SpriteChoice choice = _sprite.SelectedItem as SpriteChoice ?? throw new InvalidDataException("Select a sprite for the frame.");
        AnimationFrame frame = asset.Frames[_editingFrame] with
        {
            AtlasId = choice.AtlasId,
            SpriteId = choice.SpriteId,
            DurationTicks = EditorInput.Integer(_duration, "frame duration")
        };
        return asset with
        {
            Name = _name.Text ?? "",
            TicksPerSecond = EditorInput.Integer(_timebase, "ticks per second"),
            Repeat = _repeat.IsChecked == true,
            Frames = asset.Frames.SetItem(_editingFrame, frame)
        };
    }
    public void Accept(AnimationAsset asset) => _asset = asset;
    public void Load(AnimationAsset asset, ProjectDocument document)
    {
        int index = _asset?.Id == asset.Id ? _editingFrame : 0;
        _loading = true;
        try
        {
            _asset = asset;
            _name.Text = asset.Name;
            _timebase.Value = asset.TicksPerSecond;
            _repeat.IsChecked = asset.Repeat;
            SpriteChoice[] choices = [.. document.Atlases.OrderBy(a => a.Id).SelectMany(a => a.Sprites.Select(s => new SpriteChoice(a.Id, s.Id, a.Name + "/" + s.Name)))];
            _sprite.ItemsSource = choices;
            _frames.ItemsSource = asset.Frames.Select((frame, i) => $"{i + 1}: {choices.First(c => c.AtlasId == frame.AtlasId && c.SpriteId == frame.SpriteId).Name} · {frame.DurationTicks} ticks").ToArray();
            _frames.SelectedIndex = Math.Min(index, asset.Frames.Length - 1);
            LoadFrame(_frames.SelectedIndex);
        }
        finally { _loading = false; }
    }
    private void LoadFrame(int index)
    {
        if (_asset is null)
        {
            return;
        }
        _loading = true;
        try
        {
            _editingFrame = index;
            AnimationFrame frame = _asset.Frames[index];
            _duration.Value = frame.DurationTicks;
            _sprite.SelectedItem = _sprite.Items.OfType<SpriteChoice>().First(c => c.AtlasId == frame.AtlasId && c.SpriteId == frame.SpriteId);
            _actions["Remove"].IsEnabled = _asset.Frames.Length > 1;
            _actions["Up"].IsEnabled = index > 0;
            _actions["Down"].IsEnabled = index < _asset.Frames.Length - 1;
        }
        finally { _loading = false; }
    }
    private sealed record SpriteChoice(Guid AtlasId, Guid SpriteId, string Name)
    {
        public override string ToString() => Name;
    }
}
