using System.Collections.Immutable;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio;

internal sealed class AtlasEditor : StackPanel
{
    private readonly TextBox _name = new() { Name = "AtlasName" };
    private readonly ComboBox _texture = new() { Name = "AtlasTexture", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ListBox _sprites = new() { Name = "SpriteList", Height = 130 };
    private readonly TextBox _spriteName = new() { Name = "SpriteName" };
    private readonly NumericUpDown[] _rectangle = Enumerable.Range(0, 6).Select(i => new NumericUpDown
    {
        Minimum = i >= 4 ? -4096 : i >= 2 ? 1 : 0,
        Maximum = 4096,
        Increment = 1,
        FormatString = "0",
        ParsingNumberStyle = NumberStyles.Integer,
        Name = new[] { "SpriteX", "SpriteY", "SpriteWidth", "SpriteHeight", "PivotX", "PivotY" }[i],
    }).ToArray();
    private AtlasAsset? _asset;
    private AtlasAsset? _draft;
    private Guid _displayedSprite;
    private readonly TextBlock _error = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private bool _loading;

    public AtlasEditor(Func<AtlasAsset, Task> edit, Action<Guid> selected)
    {
        Spacing = 8;
        Children.Add(new TextBlock { Text = "Atlas name" });
        Children.Add(_name);
        Children.Add(new TextBlock { Text = "Texture" });
        Children.Add(_texture);
        Children.Add(_sprites);
        Children.Add(_error);
        Children.Add(new TextBlock { Text = "Sprite name" });
        Children.Add(_spriteName);
        string[] labels = ["Left", "Top", "Width", "Height", "Pivot X (pixels)", "Pivot Y (pixels)"];
        for (int i = 0; i < labels.Length; i++)
        {
            Children.Add(new TextBlock { Text = labels[i] });
            Children.Add(_rectangle[i]);
        }

        Button apply = new()
        {
            Content = "Apply sprite edits",
            Name = "ApplySpriteEdits"
        };
        apply.Click += async (_, _) =>
        {
            if (_asset is not null)
            {
                await edit(PendingEdits());
            }
        };
        Button add = new()
        {
            Content = "Add sprite",
            Name = "AddSprite"
        };
        add.Click += async (_, _) =>
        {
            if (_asset is not null)
            {
                AtlasAsset draft = PendingEdits();
                SpriteRegion region = draft.Sprites[0] with
                {
                    Id = Guid.NewGuid(),
                    Name = "Sprite " + (draft.Sprites.Length + 1)
                };
                await edit(draft with { Sprites = [.. draft.Sprites, region] });
                Select(region.Id);
            }
        };
        Button remove = new()
        {
            Content = "Remove sprite",
            Name = "RemoveSprite"
        };
        remove.Click += async (_, _) =>
        {
            if (_asset is not null && _sprites.SelectedItem is SpriteRow row)
            {
                AtlasAsset draft = PendingEdits();
                await edit(draft with { Sprites = [.. draft.Sprites.Where(s => s.Id != row.Region.Id)] });
            }
        };
        Children.Add(apply);
        Children.Add(add);
        Children.Add(remove);
        _sprites.SelectionChanged += (_, _) =>
        {
            if (_loading || _sprites.SelectedItem is not SpriteRow row)
            {
                return;
            }

            try
            {
                _draft = PendingEdits();
                _error.Text = "";
            }
            catch (InvalidDataException error)
            {
                _error.Text = error.Message;
                _loading = true;
                try { _sprites.SelectedItem = _sprites.Items.OfType<SpriteRow>().FirstOrDefault(s => s.Region.Id == _displayedSprite); }
                finally { _loading = false; }
                return;
            }

            _displayedSprite = row.Region.Id;
            ShowSprite(row.Region.Id);
            selected(row.Region.Id);
        };
    }

    private int Number(int index)
    {
        decimal value = _rectangle[index].Value ?? throw new InvalidDataException("Enter a whole number for each sprite coordinate and size.");
        if (value != decimal.Truncate(value))
        {
            throw new InvalidDataException("Sprite coordinates and sizes must be whole numbers.");
        }
        return decimal.ToInt32(value);
    }

    public bool HasPendingEdits
    {
        get
        {
            if (_asset is null)
            {
                return false;
            }
            try
            {
                AtlasAsset draft = PendingEdits();
                return draft.Name != _asset.Name || draft.TextureId != _asset.TextureId || !draft.Sprites.SequenceEqual(_asset.Sprites);
            }
            catch (InvalidDataException)
            {
                return true;
            }
        }
    }

    public AtlasAsset PendingEdits()
    {
        AtlasAsset draft = _draft ?? throw new InvalidOperationException("Select an atlas first.");
        if (_texture.SelectedItem is not ResourceInfo texture)
        {
            throw new InvalidDataException("Select a texture for this atlas.");
        }
        SpriteRegion current = draft.Sprites.Single(s => s.Id == _displayedSprite);
        SpriteRegion region = current with
        {
            Name = _spriteName.Text ?? "",
            X = Number(0),
            Y = Number(1),
            Width = Number(2),
            Height = Number(3),
            PivotX = Number(4),
            PivotY = Number(5)
        };
        return draft with
        {
            Name = _name.Text ?? "",
            TextureId = texture.Id,
            Sprites = [.. draft.Sprites.Select(s => s.Id == region.Id ? region : s)]
        };
    }

    private void ShowSprite(Guid id)
    {
        SpriteRegion region = _draft!.Sprites.Single(s => s.Id == id);
        _spriteName.Text = region.Name;
        int[] values = [region.X, region.Y, region.Width, region.Height, region.PivotX, region.PivotY];
        for (int i = 0; i < values.Length; i++)
        {
            _rectangle[i].Value = values[i];
        }
    }

    public void Load(AtlasAsset asset, ProjectDocument document)
    {
        Guid? selected = (_sprites.SelectedItem as SpriteRow)?.Region.Id;
        _loading = true;
        try
        {
            _asset = asset;
            _draft = asset;
            _error.Text = "";
            _name.Text = asset.Name;
            ResourceInfo[] textures = document.Resources.Where(a => a.Kind == "texture").ToArray();
            _texture.ItemsSource = textures;
            _texture.SelectedItem = textures.First(t => t.Id == asset.TextureId);
            SpriteRow[] rows = asset.Sprites.Select(s => new SpriteRow(s)).ToArray();
            _sprites.ItemsSource = rows;
            _sprites.SelectedItem = rows.FirstOrDefault(r => r.Region.Id == selected) ?? rows[0];
            _displayedSprite = ((SpriteRow)_sprites.SelectedItem!).Region.Id;
            ShowSprite(_displayedSprite);
        }
        finally
        {
            _loading = false;
        }
    }

    public Guid SelectedSprite => (_sprites.SelectedItem as SpriteRow)?.Region.Id ?? Guid.Empty;
    private void Select(Guid id) => _sprites.SelectedItem = _sprites.Items.OfType<SpriteRow>().FirstOrDefault(s => s.Region.Id == id);
    private sealed record SpriteRow(SpriteRegion Region)
    {
        public override string ToString() => $"{Region.Name} · {Region.Width} × {Region.Height}";
    }
}

internal sealed class AnimationEditor : StackPanel
{
    private readonly TextBox _name = new() { Name = "AnimationName" };
    private readonly NumericUpDown _timebase = new() { Minimum = 1, Maximum = 1_000_000, Increment = 1, FormatString = "0", Name = "AnimationTimebase" };
    private readonly CheckBox _repeat = new() { Content = "Repeat", Name = "AnimationRepeat" };
    private readonly ListBox _frames = new() { Height = 180, Name = "AnimationFrames" };
    private readonly ComboBox _sprite = new() { Name = "FrameSprite", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly NumericUpDown _duration = new() { Minimum = 1, Maximum = int.MaxValue, Increment = 1, FormatString = "0", Name = "FrameDuration", Value = 100 };
    private AnimationAsset? _asset;
    private AnimationAsset? _draft;
    private int _displayedFrame;
    private readonly TextBlock _error = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private bool _loading;

    public AnimationEditor(Func<AnimationAsset, Task> edit, Action<int> selected)
    {
        _timebase.ParsingNumberStyle = NumberStyles.Integer;
        _duration.ParsingNumberStyle = NumberStyles.Integer;
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
        settings.Click += async (_, _) =>
        {
            if (_asset is not null)
            {
                await edit(PendingEdits());
            }
        };
        Children.Add(settings);
        Children.Add(_frames);
        Children.Add(_error);
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
            button.Click += async (_, _) =>
            {
                if (_asset is null || _frames.SelectedIndex < 0)
                {
                    return;
                }

                int index = _frames.SelectedIndex;
                AnimationAsset draft = PendingEdits();
                ImmutableArray<AnimationFrame> frames = draft.Frames;
                if (command is "Add" or "Update" && _sprite.SelectedItem is SpriteChoice choice)
                {
                    AnimationFrame frame = new(command == "Add" ? Guid.NewGuid() : frames[index].Id, choice.AtlasId, choice.SpriteId, decimal.ToInt32(_duration.Value ?? 0));
                    frames = command == "Add" ? frames.Insert(index + 1, frame) : frames.SetItem(index, frame);
                }
                else if (command == "Remove")
                {
                    frames = frames.RemoveAt(index);
                }
                else
                {
                    int target = command == "Up" ? index - 1 : index + 1;
                    if (target < 0 || target >= frames.Length)
                    {
                        return;
                    }

                    frames = frames.SetItem(index, frames[target]).SetItem(target, frames[index]);
                    index = target;
                }

                await edit(draft with { Frames = frames });
                _frames.SelectedIndex = Math.Min(command == "Add" ? index + 1 : index, frames.Length - 1);
            };
            Children.Add(button);
        }

        _frames.SelectionChanged += (_, _) =>
        {
            if (_loading || _draft is null || _frames.SelectedIndex < 0)
            {
                return;
            }

            int next = _frames.SelectedIndex;
            try
            {
                _draft = PendingEdits();
                _error.Text = "";
            }
            catch (InvalidDataException error)
            {
                _error.Text = error.Message;
                _loading = true;
                try { _frames.SelectedIndex = _displayedFrame; }
                finally { _loading = false; }
                return;
            }

            _displayedFrame = next;
            ShowFrame(next);
            selected(next);
        };
    }

    public bool HasPendingEdits
    {
        get
        {
            if (_asset is null)
            {
                return false;
            }
            try
            {
                AnimationAsset draft = PendingEdits();
                return draft.Name != _asset.Name || draft.TicksPerSecond != _asset.TicksPerSecond ||
                    draft.Repeat != _asset.Repeat || !draft.Frames.SequenceEqual(_asset.Frames);
            }
            catch (InvalidDataException)
            {
                return true;
            }
        }
    }

    public AnimationAsset PendingEdits()
    {
        AnimationAsset draft = _draft ?? throw new InvalidOperationException("Select an animation first.");
        int timebase = WholeNumber(_timebase, "Ticks per second");
        ImmutableArray<AnimationFrame> frames = draft.Frames;
        if (_displayedFrame >= 0 && _displayedFrame < frames.Length)
        {
            if (_sprite.SelectedItem is not SpriteChoice sprite)
            {
                throw new InvalidDataException("Select a frame sprite.");
            }
            frames = frames.SetItem(_displayedFrame, frames[_displayedFrame] with
            {
                AtlasId = sprite.AtlasId,
                SpriteId = sprite.SpriteId,
                DurationTicks = WholeNumber(_duration, "Frame duration")
            });
        }
        return draft with { Name = _name.Text ?? "", TicksPerSecond = timebase, Repeat = _repeat.IsChecked == true, Frames = frames };
    }

    private static int WholeNumber(NumericUpDown field, string name)
    {
        decimal value = field.Value ?? throw new InvalidDataException(name + " must be a whole number.");
        if (value != decimal.Truncate(value))
        {
            throw new InvalidDataException(name + " must be a whole number.");
        }
        return decimal.ToInt32(value);
    }

    private void ShowFrame(int index)
    {
        if (_draft is not { } draft || index < 0 || index >= draft.Frames.Length)
        {
            return;
        }
        AnimationFrame frame = draft.Frames[index];
        _duration.Value = frame.DurationTicks;
        _sprite.SelectedItem = _sprite.Items.OfType<SpriteChoice>().FirstOrDefault(c => c.AtlasId == frame.AtlasId && c.SpriteId == frame.SpriteId);
    }

    public int SelectedFrame => Math.Max(0, _frames.SelectedIndex);

    public void Load(AnimationAsset asset, ProjectDocument document)
    {
        int index = _asset?.Id == asset.Id ? Math.Max(0, _frames.SelectedIndex) : 0;
        _loading = true;
        try
        {
            _asset = asset;
            _draft = asset;
            _error.Text = "";
            _name.Text = asset.Name;
            _timebase.Value = asset.TicksPerSecond;
            _repeat.IsChecked = asset.Repeat;
            SpriteChoice[] choices = document.Atlases.OrderBy(a => a.Id).SelectMany(a => a.Sprites.Select(s => new SpriteChoice(a.Id, s.Id, a.Name + "/" + s.Name))).ToArray();
            _sprite.ItemsSource = choices;
            _frames.ItemsSource = asset.Frames.Select((frame, i) => $"{i + 1}: {choices.First(c => c.AtlasId == frame.AtlasId && c.SpriteId == frame.SpriteId).Name} · {frame.DurationTicks} ticks").ToArray();
            _frames.SelectedIndex = Math.Min(index, asset.Frames.Length - 1);
            _displayedFrame = _frames.SelectedIndex;
            ShowFrame(_displayedFrame);
        }
        finally
        {
            _loading = false;
        }
    }

    private sealed record SpriteChoice(Guid AtlasId, Guid SpriteId, string Name)
    {
        public override string ToString() => Name;
    }
}
