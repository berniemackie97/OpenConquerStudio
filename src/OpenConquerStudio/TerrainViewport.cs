using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using OpenConquerStudio.Authoring;

namespace OpenConquerStudio;

internal sealed class TerrainViewport : Grid
{
    private readonly TerrainGridSurface _grid = new();
    private readonly NumericUpDown _startX = Number("TerrainSelectionStartX");
    private readonly NumericUpDown _startY = Number("TerrainSelectionStartY");
    private readonly NumericUpDown _endX = Number("TerrainSelectionEndX");
    private readonly NumericUpDown _endY = Number("TerrainSelectionEndY");
    private readonly NumericUpDown _originX = Number("TerrainViewOriginX");
    private readonly NumericUpDown _originY = Number("TerrainViewOriginY");
    private readonly Slider _zoom = new() { Name = "TerrainGridZoom", Minimum = 12, Maximum = 48, Value = 28, Width = 110 };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private TerrainAsset? _asset;
    private bool _loading;

    public event Func<Task>? PaintRequested;

    public TerrainRegion Selection
    {
        get
        {
            TerrainRegion region = TerrainRegion.Between(
                EditorInput.Integer(_startX, "selection start X"), EditorInput.Integer(_startY, "selection start Y"),
                EditorInput.Integer(_endX, "selection end X"), EditorInput.Integer(_endY, "selection end Y"));
            region.Validate(_asset?.Width ?? 0, _asset?.Height ?? 0);
            return region;
        }
    }

    public TerrainViewport()
    {
        RowDefinitions = new RowDefinitions("Auto,*,Auto");
        RowSpacing = 6;
        Margin = new Thickness(12);
        WrapPanel controls = new()
        {
            ItemSpacing = 8,
            LineSpacing = 6
        };
        foreach ((string label, Control input) in new (string, Control)[]
        {
            ("Select X0", _startX), ("Y0", _startY), ("X1", _endX), ("Y1", _endY),
            ("View X", _originX), ("Y", _originY), ("Cell size", _zoom),
        })
        {
            controls.Children.Add(new TextBlock { Text = label, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
            controls.Children.Add(input);
        }

        Button paint = new()
        {
            Name = "PaintTerrainSelection",
            Content = "Paint selected cells"
        };
        paint.Click += async (_, _) =>
        {
            if (PaintRequested is { } apply)
            {
                await apply();
            }
        };
        controls.Children.Add(paint);
        Children.Add(controls);
        Grid.SetRow(_grid, 1);
        Children.Add(_grid);
        _status.Text = "Raw cell-grid view. Colors distinguish surface IDs; they are not game textures or traversal semantics.";
        Grid.SetRow(_status, 2);
        Children.Add(_status);

        _grid.Selected += region =>
        {
            _loading = true;
            try
            {
                _startX.Value = region.StartX;
                _startY.Value = region.StartY;
                _endX.Value = region.EndX;
                _endY.Value = region.EndY;
            }
            finally { _loading = false; }
            UpdateStatus(region);
        };
        foreach (NumericUpDown input in new[] { _startX, _startY, _endX, _endY, _originX, _originY })
        {
            input.PropertyChanged += (_, e) =>
            {
                if (!_loading && e.Property == NumericUpDown.TextProperty)
                {
                    RefreshView();
                }
            };
        }
        _zoom.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
            {
                RefreshView();
            }
        };
        IsVisible = false;
    }

    public void Load(TerrainAsset asset, Func<Guid, int, int, TerrainCell> read)
    {
        bool changed = _asset?.Id != asset.Id;
        _asset = asset;
        _loading = true;
        try
        {
            foreach (NumericUpDown input in new[] { _startX, _endX, _originX })
            {
                input.Maximum = asset.Width - 1;
            }
            foreach (NumericUpDown input in new[] { _startY, _endY, _originY })
            {
                input.Maximum = asset.Height - 1;
            }
            if (changed)
            {
                foreach (NumericUpDown input in new[] { _startX, _startY, _endX, _endY, _originX, _originY })
                {
                    input.Value = 0;
                }
            }
        }
        finally { _loading = false; }

        _grid.Load(asset, (x, y) => read(asset.Id, x, y));
        RefreshView();
    }

    public void Clear()
    {
        _asset = null;
        _grid.Clear();
        IsVisible = false;
    }

    private void RefreshView()
    {
        if (_asset is null || _loading)
        {
            return;
        }
        try
        {
            _grid.SetView(EditorInput.Integer(_originX, "view X"),
                EditorInput.Integer(_originY, "view Y"), (int)Math.Round(_zoom.Value));
            TerrainRegion region = Selection;
            _grid.SetSelection(region);
            UpdateStatus(region);
        }
        catch (InvalidDataException error)
        {
            _status.Text = error.Message;
        }
    }

    private void UpdateStatus(TerrainRegion region)
    {
        _status.Text = $"MapDataId {_asset!.MapDataId} · {_asset.Width} × {_asset.Height} · " +
            $"Selected ({region.StartX}, {region.StartY})–({region.EndX}, {region.EndY}). " +
            "Brush values are taken from the terrain inspector. Surface colors group raw IDs, not game textures.";
    }

    private static NumericUpDown Number(string name) => new()
    {
        Name = name,
        Width = 70,
        Minimum = 0,
        Maximum = 1023,
        Increment = 1,
        FormatString = "0",
        ParsingNumberStyle = NumberStyles.Integer
    };
}
