using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
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
    private readonly Func<Guid, int, int, TerrainCell> _read;
    private TerrainAsset? _asset;
    private TerrainCell _selectedCell;
    private int _selectedX;
    private int _selectedY;
    private bool _loading;

    public event Action? PendingChanged;

    public TerrainEditor(Func<Task> apply, Func<Task<bool>> navigate, Func<Guid, int, int, TerrainCell> read)
    {
        _read = read;
        Spacing = 8;
        Children.Add(new TextBlock { Text = "Terrain", FontSize = 16 });
        Children.Add(new TextBlock { Text = "Name" });
        Children.Add(_name);
        Children.Add(_location);
        Children.Add(new TextBlock { Text = "Cell X / Y" });
        Children.Add(_x);
        Children.Add(_y);
        Button go = new() { Content = "Inspect cell", Name = "InspectTerrainCell" };
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
        Button save = new() { Content = "Apply terrain edits", Name = "ApplyTerrainEdits" };
        save.Click += async (_, _) => { if (_asset is not null) { await apply(); } };
        Children.Add(save);

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
            try
            {
                (string name, TerrainCell cell) = ReadChanges();
                return name != _asset.Name || cell != _selectedCell;
            }
            catch (InvalidDataException) { return true; }
        }
    }

    public (string Name, TerrainCell Cell) ReadChanges() =>
        (_name.Text ?? "", new TerrainCell(
            checked((ushort)EditorInput.Integer(_surface, "surface ID")),
            checked((ushort)EditorInput.Integer(_passability, "passability flag")),
            checked((short)EditorInput.Integer(_elevation, "elevation"))));

    public void Accept(TerrainAsset asset)
    {
        _asset = asset;
        _selectedCell = _read(asset.Id, _selectedX, _selectedY);
    }

    public void Load(TerrainAsset asset)
    {
        if (_asset?.Id != asset.Id)
        {
            _selectedX = 0;
            _selectedY = 0;
        }
        _loading = true;
        try
        {
            _asset = asset;
            _x.Maximum = asset.Width - 1;
            _y.Maximum = asset.Height - 1;
            _name.Text = asset.Name;
            LoadCell();
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

    private static NumericUpDown Number(string name, decimal minimum, decimal maximum) => new()
    {
        Name = name, Minimum = minimum, Maximum = maximum, Increment = 1, FormatString = "0",
        ParsingNumberStyle = NumberStyles.Integer,
    };
}
