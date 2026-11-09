using System.Collections.Immutable;
using System.Globalization;
using Avalonia.Controls;
using OpenConquerStudio.Authoring;

namespace OpenConquerStudio;

internal sealed class TerrainSceneryEditor : StackPanel
{
    private readonly ListBox _placements = new() { Name = "TerrainSceneryPlacements", Height = 130 };
    private readonly NumericUpDown _x = Number("TerrainSceneryX");
    private readonly NumericUpDown _y = Number("TerrainSceneryY");
    private readonly Func<Task<bool>> _commitForNavigation;
    private TerrainAsset? _asset;
    private TerrainSceneryPlacement? _original;
    private Guid? _activeId;
    private bool _loading;

    public event Action? PendingChanged;

    public TerrainSceneryEditor(Func<Task> apply, Func<Task<bool>> commitForNavigation)
    {
        _commitForNavigation = commitForNavigation;
        Spacing = 6;
        Children.Add(new TextBlock { Text = "Source-backed scenery placements", FontSize = 16 });
        Children.Add(new TextBlock
        {
            Text = "Editable coordinates for scenery types 1 and 4; unrecognized source records remain unchanged."
        });
        Children.Add(_placements);
        Button inspect = new() { Name = "InspectTerrainScenery", Content = "Inspect selected scenery" };
        inspect.Click += async (_, _) => await InspectSelected();
        Children.Add(inspect);
        Children.Add(new TextBlock { Text = "Position X (signed)" });
        Children.Add(_x);
        Children.Add(new TextBlock { Text = "Position Y (signed)" });
        Children.Add(_y);
        Button save = new() { Name = "ApplyTerrainScenery", Content = "Apply scenery coordinates" };
        save.Click += async (_, _) => await apply();
        Children.Add(save);
        foreach (NumericUpDown input in new[] { _x, _y })
        {
            input.PropertyChanged += (_, e) =>
            {
                if (!_loading && e.Property == NumericUpDown.TextProperty)
                {
                    PendingChanged?.Invoke();
                }
            };
        }
    }

    public bool HasPending
    {
        get
        {
            if (_original is not TerrainSceneryPlacement original)
            {
                return false;
            }
            try
            {
                return EditorInput.Integer(_x, "scenery X") != original.X ||
                    EditorInput.Integer(_y, "scenery Y") != original.Y;
            }
            catch (InvalidDataException) { return true; }
        }
    }

    public ImmutableArray<TerrainSceneryPlacement> ReadChanges(TerrainAsset asset)
    {
        if (_original is not TerrainSceneryPlacement original)
        {
            return asset.SceneryPlacements;
        }

        int x = EditorInput.Integer(_x, "scenery X");
        int y = EditorInput.Integer(_y, "scenery Y");
        if (x == original.X && y == original.Y)
        {
            return asset.SceneryPlacements;
        }
        int index = IndexOf(asset.SceneryPlacements, original.Id);
        if (index < 0)
        {
            throw new InvalidDataException("The selected scenery placement no longer exists.");
        }

        return asset.SceneryPlacements.SetItem(index, original with { X = x, Y = y });
    }

    public void Accept(TerrainAsset asset)
    {
        _asset = asset;
        _original = asset.SceneryPlacements.FirstOrDefault(item => item.Id == _activeId);
    }

    public void Load(TerrainAsset asset)
    {
        if (_asset?.Id != asset.Id)
        {
            _activeId = null;
        }
        _asset = asset;
        if (!asset.SceneryPlacements.Any(item => item.Id == _activeId))
        {
            _activeId = asset.SceneryPlacements.IsEmpty ? null : asset.SceneryPlacements[0].Id;
        }

        _loading = true;
        try
        {
            _placements.ItemsSource = asset.SceneryPlacements.Select(item =>
                $"{item.SourceIndex + 1}: type {item.TypeCode} · {item.ResourcePath} · ({item.X}, {item.Y})").ToArray();
            _placements.SelectedIndex = IndexOf(asset.SceneryPlacements, _activeId);
            IsVisible = !asset.SceneryPlacements.IsEmpty;
            LoadFields();
        }
        finally { _loading = false; }
    }

    private async Task InspectSelected()
    {
        if (_asset is null || _placements.SelectedIndex < 0)
        {
            return;
        }
        Guid requested = _asset.SceneryPlacements[_placements.SelectedIndex].Id;
        if (requested == _activeId)
        {
            return;
        }
        if (!await _commitForNavigation())
        {
            _placements.SelectedIndex = IndexOf(_asset.SceneryPlacements, _activeId);
            return;
        }
        _activeId = requested;
        _placements.SelectedIndex = IndexOf(_asset.SceneryPlacements, requested);
        LoadFields();
    }

    private void LoadFields()
    {
        _loading = true;
        try
        {
            _original = _asset?.SceneryPlacements.FirstOrDefault(item => item.Id == _activeId);
            _x.IsEnabled = _original is not null;
            _y.IsEnabled = _original is not null;
            _x.Value = _original?.X ?? 0;
            _y.Value = _original?.Y ?? 0;
        }
        finally { _loading = false; }
    }

    private static int IndexOf(ImmutableArray<TerrainSceneryPlacement> placements, Guid? id)
    {
        for (int i = 0; i < placements.Length; i++)
        {
            if (placements[i].Id == id)
            {
                return i;
            }
        }
        return -1;
    }

    private static NumericUpDown Number(string name) => new()
    {
        Name = name,
        Minimum = int.MinValue,
        Maximum = int.MaxValue,
        Increment = 1,
        FormatString = "0",
        ParsingNumberStyle = NumberStyles.Integer
    };
}
