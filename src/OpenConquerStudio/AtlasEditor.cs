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
    private readonly Action<Guid> _selected;
    private readonly Button _remove = new() { Content = "Remove sprite", Name = "RemoveSprite" };
    private AtlasAsset? _asset;
    private ProjectDocument? _document;
    private Guid _editingSprite;
    private bool _loading;

    public event Action? PendingChanged;

    public AtlasEditor(Func<Func<AtlasAsset>, Task> edit, Action<Guid> selected, Func<Task<bool>> navigate)
    {
        _selected = selected;
        Spacing = 8;
        Children.Add(new TextBlock { Text = "Atlas name" });
        Children.Add(_name);
        Children.Add(new TextBlock { Text = "Texture" });
        Children.Add(_texture);
        Children.Add(_sprites);
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
        apply.Click += async (_, _) => { if (_asset is not null) { await edit(ReadChanges); } };
        Button add = new()
        {
            Content = "Add sprite",
            Name = "AddSprite"
        };
        add.Click += async (_, _) =>
        {
            if (_asset is null)
            {
                return;
            }
            Guid id = Guid.NewGuid();
            await edit(() =>
            {
                AtlasAsset asset = ReadChanges();
                SpriteRegion region = asset.Sprites.First(s => s.Id == _editingSprite) with
                {
                    Id = id,
                    Name = "Sprite " + (asset.Sprites.Length + 1)
                };
                return asset with
                {
                    Sprites = [.. asset.Sprites, region]
                };
            });
            if (_asset.Sprites.Any(s => s.Id == id))
            {
                Select(id);
            }
        };
        _remove.Click += async (_, _) =>
        {
            if (_asset is null)
            {
                return;
            }
            await edit(() => ReadChanges() with { Sprites = [.. _asset.Sprites.Where(s => s.Id != _editingSprite)] });
        };
        Children.Add(apply);
        Children.Add(add);
        Children.Add(_remove);
        _sprites.SelectionChanged += async (_, _) =>
        {
            if (_loading || _sprites.SelectedItem is not SpriteRow row)
            {
                return;
            }
            Guid previous = _editingSprite;
            if (row.Region.Id != previous && !await navigate())
            {
                RestoreSelection(previous);
                return;
            }
            RestoreSelection(row.Region.Id);
            LoadFields(row.Region.Id);
            selected(row.Region.Id);
        };
        _name.TextChanged += (_, _) => Changed();
        _spriteName.TextChanged += (_, _) => Changed();
        _texture.SelectionChanged += (_, _) => Changed();
        foreach (NumericUpDown input in _rectangle)
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
    public Guid SelectedSprite => _editingSprite;
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
                AtlasAsset draft = ReadChanges();
                return draft.Name != _asset.Name || draft.TextureId != _asset.TextureId || !draft.Sprites.SequenceEqual(_asset.Sprites);
            }
            catch (InvalidDataException) { return true; }
        }
    }

    public AtlasAsset ReadChanges()
    {
        AtlasAsset asset = _asset ?? throw new InvalidOperationException("Select an atlas first.");
        ResourceInfo texture = _texture.SelectedItem as ResourceInfo ?? throw new InvalidDataException("Select a texture for the atlas.");
        SpriteRegion region = asset.Sprites.First(s => s.Id == _editingSprite) with
        {
            Name = _spriteName.Text ?? "",
            X = EditorInput.Integer(_rectangle[0], "sprite left"),
            Y = EditorInput.Integer(_rectangle[1], "sprite top"),
            Width = EditorInput.Integer(_rectangle[2], "sprite width"),
            Height = EditorInput.Integer(_rectangle[3], "sprite height"),
            PivotX = EditorInput.Integer(_rectangle[4], "sprite pivot X"),
            PivotY = EditorInput.Integer(_rectangle[5], "sprite pivot Y"),
        };
        return asset with
        {
            Name = _name.Text ?? "",
            TextureId = texture.Id,
            Sprites = [.. asset.Sprites.Select(s => s.Id == region.Id ? region : s)]
        };
    }

    public void Accept(AtlasAsset asset, ProjectDocument document)
    {
        _asset = asset;
        _document = document;
    }

    public void Load(AtlasAsset asset, ProjectDocument document)
    {
        Guid selected = _asset?.Id == asset.Id ? _editingSprite : Guid.Empty;
        _loading = true;
        try
        {
            Accept(asset, document);
            _name.Text = asset.Name;
            ResourceInfo[] textures = document.Resources.Where(a => a.Kind == "texture").ToArray();
            _texture.ItemsSource = textures;
            _texture.SelectedItem = textures.First(t => t.Id == asset.TextureId);
            SpriteRow[] rows = [.. asset.Sprites.Select(s => new SpriteRow(s))];
            _sprites.ItemsSource = rows;
            _sprites.SelectedItem = rows.FirstOrDefault(r => r.Region.Id == selected) ?? rows[0];
            LoadFields(((SpriteRow)_sprites.SelectedItem).Region.Id);
        }
        finally { _loading = false; }
    }

    private void LoadFields(Guid id)
    {
        if (_asset is null)
        {
            return;
        }
        SpriteRegion region = _asset.Sprites.First(s => s.Id == id);
        _loading = true;
        try
        {
            _editingSprite = id;
            _spriteName.Text = region.Name;
            int[] values = [region.X, region.Y, region.Width, region.Height, region.PivotX, region.PivotY];
            for (int i = 0; i < values.Length; i++)
            {
                _rectangle[i].Value = values[i];
            }
            _remove.IsEnabled = _asset.Sprites.Length > 1 && _document?.Animations.Any(a => a.Frames.Any(f => f.AtlasId == _asset.Id && f.SpriteId == id)) != true;
            ToolTip.SetTip(_remove, _remove.IsEnabled ? "Remove this sprite" : "Keep at least one sprite and remove animation references before deleting it.");
        }
        finally { _loading = false; }
    }

    private void RestoreSelection(Guid id)
    {
        _loading = true;
        try
        {
            _sprites.SelectedItem = _sprites.Items.OfType<SpriteRow>().FirstOrDefault(s => s.Region.Id == id);
        }
        finally { _loading = false; }
    }
    private void Select(Guid id)
    {
        RestoreSelection(id);
        if (_asset?.Sprites.Any(s => s.Id == id) == true)
        {
            LoadFields(id);
            _selected(id);
        }
    }
    private sealed record SpriteRow(SpriteRegion Region)
    {
        public override string ToString() => $"{Region.Name} · {Region.Width} × {Region.Height}";
    }
}

