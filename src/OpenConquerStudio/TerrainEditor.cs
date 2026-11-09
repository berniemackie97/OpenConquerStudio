using System.Collections.Immutable;
using System.Globalization;
using Avalonia.Controls;
using OpenConquerStudio.Authoring;

namespace OpenConquerStudio;

internal sealed class TerrainEditor : StackPanel
{
    private readonly TextBox _name = new() { Name = "TerrainName" };
    private readonly NumericUpDown _x = Number("TerrainCellX", 0, 1023);
    private readonly NumericUpDown _y = Number("TerrainCellY", 0, 1023);
    private readonly NumericUpDown _surface = Number("TerrainSurface", 0, ushort.MaxValue);
    private readonly NumericUpDown _passability = Number("TerrainPassability", 0, ushort.MaxValue);
    private readonly NumericUpDown _elevation = Number("TerrainElevation", short.MinValue, short.MaxValue);
    private readonly TextBlock _location = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _sourceFrameHeading = new() { Text = "Captured image frames" };
    private readonly Button _promoteButton = new()
    {
        Name = "PromoteTerrainFrames",
        Content = "Create editable textures from selected frames"
    };
    private readonly ListBox _sourceFrames = new()
    {
        Name = "TerrainSourceFrameSelection",
        Height = 150,
        SelectionMode = SelectionMode.Multiple
    };
    private readonly Func<Guid, int, int, TerrainCell> _read;
    private readonly TerrainExitEditor _exitEditor;
    private readonly TerrainSceneryEditor _sceneryEditor;
    private readonly Func<Guid, ImmutableArray<string>, Task> _promoteFrames;
    private TerrainAsset? _asset;
    private TerrainCell _selectedCell;
    private int _selectedX;
    private int _selectedY;
    private bool _loading;

    public event Action? PendingChanged;

    public TerrainEditor(Func<Task> apply, Func<Task<bool>> navigate, Func<Guid, int, int, TerrainCell> read,
        Func<Guid, ImmutableArray<TerrainExitMarker>, Task> replaceExits,
        Func<Guid, ImmutableArray<string>, Task> promoteFrames)
    {
        _read = read;
        _promoteFrames = promoteFrames;
        _exitEditor = new TerrainExitEditor(apply, navigate, replaceExits);
        _exitEditor.PendingChanged += Changed;
        _sceneryEditor = new TerrainSceneryEditor(apply, navigate);
        _sceneryEditor.PendingChanged += Changed;
        Spacing = 8;
        Children.Add(new TextBlock { Text = "Terrain", FontSize = 16 });
        Children.Add(new TextBlock { Text = "Name" });
        Children.Add(_name);
        Children.Add(_location);
        Children.Add(_sourceFrameHeading);
        Children.Add(_sourceFrames);
        _sourceFrames.SelectionChanged += (_, _) =>
            _promoteButton.IsEnabled = _sourceFrames.SelectedItems?.Count > 0;
        _promoteButton.Click += async (_, _) =>
        {
            if (_asset is not null)
            {
                ImmutableArray<string> paths = _sourceFrames.SelectedItems is { } selected
                    ? [.. selected.OfType<SourceFrameOption>().Select(item => item.Path)] : [];
                if (!paths.IsEmpty)
                {
                    await _promoteFrames(_asset.Id, paths);
                }
            }
        };
        Children.Add(_promoteButton);
        Children.Add(new TextBlock { Text = "Cell X / Y" });
        Children.Add(_x);
        Children.Add(_y);
        Button go = new()
        {
            Content = "Inspect cell",
            Name = "InspectTerrainCell"
        };
        go.Click += async (_, _) =>
        {
            if (_asset is null)
            {
                return;
            }
            try
            {
                int x = EditorInput.Integer(_x, "cell X");
                int y = EditorInput.Integer(_y, "cell Y");
                if ((x != _selectedX || y != _selectedY) && await navigate())
                {
                    _selectedX = x;
                    _selectedY = y;
                    LoadCell();
                }
            }
            catch (InvalidDataException error) { _location.Text = error.Message; }
        };
        Children.Add(go);
        Children.Add(new TextBlock { Text = "Surface ID" });
        Children.Add(_surface);
        Children.Add(new TextBlock { Text = "Passability flag (raw)" });
        Children.Add(_passability);
        Children.Add(new TextBlock { Text = "Elevation (signed)" });
        Children.Add(_elevation);
        Button save = new()
        {
            Content = "Apply terrain edits",
            Name = "ApplyTerrainEdits"
        };
        save.Click += async (_, _) => { if (_asset is not null) { await apply(); } };
        Children.Add(save);
        Children.Add(_exitEditor);
        Children.Add(_sceneryEditor);

        _name.TextChanged += (_, _) => Changed();
        foreach (NumericUpDown input in new[] { _surface, _passability, _elevation })
        {
            input.PropertyChanged += (_, e) => { if (e.Property == NumericUpDown.TextProperty) { Changed(); } };
        }
        IsVisible = false;
    }

    public int SelectedX => _selectedX;
    public int SelectedY => _selectedY;

    public bool HasPending
    {
        get
        {
            if (_asset is null)
            {
                return false;
            }
            if (_exitEditor.HasPending || _sceneryEditor.HasPending)
            {
                return true;
            }
            try
            {
                var changes = ReadChanges();
                return changes.Name != _asset.Name || changes.Cell != _selectedCell;
            }
            catch (InvalidDataException) { return true; }
        }
    }

    public (string Name, TerrainCell Cell, ImmutableArray<TerrainExitMarker> Exits,
        ImmutableArray<TerrainSceneryPlacement> SceneryPlacements) ReadChanges()
    {
        TerrainAsset asset = _asset ?? throw new InvalidOperationException("Select a terrain before editing its cells.");
        return (_name.Text ?? "", new TerrainCell(
            checked((ushort)EditorInput.Integer(_surface, "surface ID")),
            checked((ushort)EditorInput.Integer(_passability, "passability flag")),
            checked((short)EditorInput.Integer(_elevation, "elevation"))),
            _exitEditor.ReadChanges(asset), _sceneryEditor.ReadChanges(asset));
    }

    public void Accept(TerrainAsset asset)
    {
        _asset = asset;
        _selectedCell = _read(asset.Id, _selectedX, _selectedY);
        _exitEditor.Accept(asset);
        _sceneryEditor.Accept(asset);
    }

    public void Load(TerrainAsset asset)
    {
        if (_asset?.Id != asset.Id)
        {
            _selectedX = 0;
            _selectedY = 0;
        }
        else
        {
            _selectedX = Math.Min(_selectedX, asset.Width - 1);
            _selectedY = Math.Min(_selectedY, asset.Height - 1);
        }
        _loading = true;
        try
        {
            _asset = asset;
            _x.Maximum = asset.Width - 1;
            _y.Maximum = asset.Height - 1;
            _name.Text = asset.Name;
            _sourceFrames.ItemsSource = asset.Source?.Frames.Select(frame =>
                new SourceFrameOption(frame.Path, frame.TextureId)).ToArray() ?? [];
            bool hasSourceFrames = asset.Source?.Frames.IsEmpty == false;
            _sourceFrameHeading.IsVisible = hasSourceFrames;
            _sourceFrames.IsVisible = hasSourceFrames;
            _promoteButton.IsVisible = hasSourceFrames;
            _promoteButton.IsEnabled = _sourceFrames.SelectedItems?.Count > 0;
            LoadCell();
            _exitEditor.Load(asset);
            _sceneryEditor.Load(asset);
        }
        finally { _loading = false; }
    }

    private void LoadCell()
    {
        if (_asset is null)
        {
            return;
        }
        _loading = true;
        try
        {
            _selectedCell = _read(_asset.Id, _selectedX, _selectedY);
            _x.Value = _selectedX;
            _y.Value = _selectedY;
            _surface.Value = _selectedCell.SurfaceId;
            _passability.Value = _selectedCell.PassabilityFlag;
            _elevation.Value = _selectedCell.Elevation;
            _location.Text = $"MapDataId {_asset.MapDataId} · {_asset.Width} × {_asset.Height}\nCell {_selectedX}, {_selectedY}";
            if (_asset.Source is { } source)
            {
                _location.Text += $"\nSource {source.MapPath} · {source.SceneryCount} scenery records · " +
                    $"{source.OverlayGroupCount} overlay groups · " +
                    (source.SnapshotDigest is null ? "source content not retained" :
                        $"decoded map retained ({source.SnapshotLength:N0} bytes)");
                if (!source.Puzzles.IsEmpty)
                {
                    _location.Text += $"\nPuzzle grids: {source.Puzzles.Count(puzzle => puzzle.Digest is not null)} captured, " +
                        $"{source.Puzzles.Count(puzzle => puzzle.Digest is null)} unavailable";
                }
                if (!source.AnimationIndexes.IsEmpty)
                {
                    _location.Text += $"\nANI indexes: {source.AnimationIndexes.Length} retained, " +
                        $"{source.AnimationIndexes.Sum(index => index.Sections.Length)} sections. " +
                        "Referenced ANI sections are verified.";
                }
                if (!source.Scenes.IsEmpty || !source.Frames.IsEmpty)
                {
                    _location.Text += $"\nScenery sources: {source.Scenes.Length}, " +
                        $"verified image frames: {source.Frames.Length}. " +
                        "Authoring layer transforms and target compilation remain outstanding.";
                }
            }
        }
        finally { _loading = false; }
    }

    private void Changed()
    {
        if (!_loading)
        {
            PendingChanged?.Invoke();
        }
    }

    private sealed record SourceFrameOption(string Path, Guid? TextureId)
    {
        public override string ToString() => TextureId is Guid
            ? $"{Path} · linked"
            : Path;
    }

    private static NumericUpDown Number(string name, decimal minimum, decimal maximum) => new()
    {
        Name = name,
        Minimum = minimum,
        Maximum = maximum,
        Increment = 1,
        FormatString = "0",
        ParsingNumberStyle = NumberStyles.Integer,
    };
}
