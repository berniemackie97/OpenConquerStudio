using System.Collections.Immutable;
using System.Globalization;
using Avalonia.Controls;
using OpenConquerStudio.Authoring;

namespace OpenConquerStudio;

internal sealed class TerrainExitEditor : StackPanel
{
    private readonly ListBox _markers = new() { Name = "TerrainExitMarkers", Height = 130 };
    private readonly NumericUpDown _x = Number("TerrainExitX", int.MinValue, int.MaxValue);
    private readonly NumericUpDown _y = Number("TerrainExitY", int.MinValue, int.MaxValue);
    private readonly NumericUpDown _passway = Number("TerrainExitPassway", 0, uint.MaxValue);
    private readonly Func<Task<bool>> _commitForNavigation;
    private readonly Func<Guid, ImmutableArray<TerrainExitMarker>, Task> _replace;
    private TerrainAsset? _asset;
    private Guid? _activeMarker;
    private TerrainExitMarker? _original;
    private bool _loading;

    public event Action? PendingChanged;

    public TerrainExitEditor(Func<Task> apply, Func<Task<bool>> commitForNavigation,
        Func<Guid, ImmutableArray<TerrainExitMarker>, Task> replace)
    {
        _commitForNavigation = commitForNavigation;
        _replace = replace;
        Spacing = 6;
        Children.Add(new TextBlock { Text = "Ordered exit markers", FontSize = 16 });
        Children.Add(new TextBlock { Text = "Raw terrain markers; destinations belong to world map definitions." });
        Children.Add(_markers);
        Button inspect = new()
        {
            Name = "InspectTerrainExit",
            Content = "Inspect selected marker"
        };
        inspect.Click += async (_, _) =>
        {
            if (_asset is null || _markers.SelectedIndex < 0)
            {
                return;
            }

            Guid requested = _asset.Exits[_markers.SelectedIndex].Id;
            if (requested != _activeMarker && await _commitForNavigation())
            {
                _activeMarker = requested;
                _markers.SelectedIndex = IndexOf(_asset.Exits, requested);
                LoadFields();
            }
        };
        Children.Add(inspect);
        Children.Add(new TextBlock { Text = "Marker X (signed)" });
        Children.Add(_x);
        Children.Add(new TextBlock { Text = "Marker Y (signed)" });
        Children.Add(_y);
        Children.Add(new TextBlock { Text = "Passway index (unsigned)" });
        Children.Add(_passway);
        Button applyMarker = new()
        {
            Name = "ApplyTerrainExit",
            Content = "Apply marker edits"
        };
        applyMarker.Click += async (_, _) => await apply();
        Children.Add(applyMarker);
        Button add = new()
        {
            Name = "AddTerrainExit",
            Content = "Add marker"
        };
        add.Click += async (_, _) =>
        {
            if (_asset is null || !await _commitForNavigation())
            {
                return;
            }

            Guid added = Guid.NewGuid();
            await _replace(_asset.Id, [.. _asset.Exits, new TerrainExitMarker(added, 0, 0, 0)]);
            if (_asset.Exits.Any(marker => marker.Id == added))
            {
                _activeMarker = added;
                _markers.SelectedIndex = IndexOf(_asset.Exits, added);
                LoadFields();
            }
        };
        Children.Add(add);
        Button remove = new()
        {
            Name = "RemoveTerrainExit",
            Content = "Remove inspected marker"
        };
        remove.Click += async (_, _) =>
        {
            if (_asset is null || _activeMarker is not Guid id || !await _commitForNavigation())
            {
                return;
            }

            await _replace(_asset.Id, [.. _asset.Exits.Where(marker => marker.Id != id)]);
        };
        Children.Add(remove);
        Button up = new()
        {
            Name = "RaiseTerrainExit",
            Content = "Move marker earlier"
        };
        Button down = new()
        {
            Name = "LowerTerrainExit",
            Content = "Move marker later"
        };
        up.Click += async (_, _) => await Move(-1);
        down.Click += async (_, _) => await Move(1);
        Children.Add(up);
        Children.Add(down);

        foreach (NumericUpDown input in new[] { _x, _y, _passway })
        {
            input.PropertyChanged += (_, change) =>
            {
                if (!_loading && change.Property == NumericUpDown.TextProperty)
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
            if (_original is not TerrainExitMarker original)
            {
                return false;
            }

            try
            {
                return ReadMarker(original) != original;
            }
            catch (InvalidDataException) { return true; }
        }
    }

    public ImmutableArray<TerrainExitMarker> ReadChanges(TerrainAsset asset)
    {
        if (_original is not TerrainExitMarker original)
        {
            return asset.Exits;
        }

        TerrainExitMarker changed = ReadMarker(original);
        if (changed == original)
        {
            return asset.Exits;
        }

        int index = IndexOf(asset.Exits, original.Id);
        if (index < 0)
        {
            throw new InvalidDataException("The selected exit marker no longer exists.");
        }

        return asset.Exits.SetItem(index, changed);
    }

    public void Accept(TerrainAsset asset)
    {
        _asset = asset;
        TerrainExitMarker marker = asset.Exits.FirstOrDefault(entry => entry.Id == _activeMarker);
        _original = marker.Id == Guid.Empty ? null : marker;
    }

    public void Load(TerrainAsset asset)
    {
        if (_asset?.Id != asset.Id)
        {
            _activeMarker = null;
        }

        _asset = asset;
        if (!asset.Exits.Any(marker => marker.Id == _activeMarker))
        {
            _activeMarker = asset.Exits.Length == 0 ? null : asset.Exits[0].Id;
        }

        _loading = true;
        try
        {
            _markers.ItemsSource = asset.Exits.Select((marker, index) =>
                $"{index + 1}: ({marker.X}, {marker.Y}) · passway {marker.PasswayIndex}").ToArray();
            _markers.SelectedIndex = IndexOf(asset.Exits, _activeMarker);
            LoadFields();
        }
        finally { _loading = false; }
    }

    private TerrainExitMarker ReadMarker(TerrainExitMarker original) => new(original.Id,
        EditorInput.Integer(_x, "exit X"), EditorInput.Integer(_y, "exit Y"),
        EditorInput.UnsignedInteger(_passway, "passway index"));

    private void LoadFields()
    {
        _loading = true;
        try
        {
            TerrainExitMarker? marker = _asset?.Exits.FirstOrDefault(entry => entry.Id == _activeMarker);
            if (marker?.Id == Guid.Empty)
            {
                marker = null;
            }

            _original = marker;
            _x.IsEnabled = marker is not null;
            _y.IsEnabled = marker is not null;
            _passway.IsEnabled = marker is not null;
            _x.Value = marker?.X ?? 0;
            _y.Value = marker?.Y ?? 0;
            _passway.Value = marker?.PasswayIndex ?? 0;
        }
        finally { _loading = false; }
    }

    private async Task Move(int step)
    {
        if (_asset is null || _activeMarker is not Guid id || !await _commitForNavigation())
        {
            return;
        }

        int index = IndexOf(_asset.Exits, id);
        int destination = index + step;
        if (index < 0 || destination < 0 || destination >= _asset.Exits.Length)
        {
            return;
        }

        TerrainExitMarker marker = _asset.Exits[index];
        await _replace(_asset.Id, _asset.Exits.RemoveAt(index).Insert(destination, marker));
    }

    private static int IndexOf(ImmutableArray<TerrainExitMarker> markers, Guid? id)
    {
        for (int index = 0; index < markers.Length; index++)
        {
            if (markers[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    private static NumericUpDown Number(string name, decimal minimum, decimal maximum) => new()
    {
        Name = name,
        Minimum = minimum,
        Maximum = maximum,
        Increment = 1,
        FormatString = "0",
        ParsingNumberStyle = NumberStyles.Integer
    };
}
