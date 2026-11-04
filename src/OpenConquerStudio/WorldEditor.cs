using Avalonia.Controls;
using OpenConquerStudio.Authoring;

namespace OpenConquerStudio;

internal sealed class WorldEditor : StackPanel
{
    private readonly TextBox _name = new() { Name = "WorldName" };
    private readonly TextBox _mapId = new() { Name = "WorldMapId" };
    private readonly TextBox _flags = new() { Name = "WorldFlags" };
    private readonly ComboBox _terrain = new() { Name = "WorldTerrain" };
    private readonly Func<Task> _apply;
    private TerrainAsset[] _terrains = [];
    private WorldAsset? _current;
    private bool _loading;

    public event Action? PendingChanged;

    public WorldEditor(Func<Task> apply)
    {
        _apply = apply;
        Spacing = 8;
        Children.Add(new TextBlock { Text = "World definition", FontSize = 16 });
        Children.Add(new TextBlock { Text = "World MapId" });
        Children.Add(_mapId);
        Children.Add(new TextBlock { Text = "World name" });
        Children.Add(_name);
        Children.Add(new TextBlock { Text = "Authored terrain (MapDataId)" });
        Children.Add(_terrain);
        Children.Add(new TextBlock { Text = "64-bit flags (decimal or 0x hex)" });
        Children.Add(_flags);
        Children.Add(new TextBlock
        {
            Text = "World identity and flags are explicit. Terrain exit markers do not define portal destinations.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });
        Button save = new()
        {
            Name = "ApplyWorldEdits",
            Content = "Apply world edits"
        };
        save.Click += async (_, _) => await _apply();
        Children.Add(save);
        _name.TextChanged += (_, _) => Changed();
        _mapId.TextChanged += (_, _) => Changed();
        _flags.TextChanged += (_, _) => Changed();
        _terrain.SelectionChanged += (_, _) => Changed();
        IsVisible = false;
    }

    public bool HasPending
    {
        get
        {
            if (_current is null)
            {
                return false;
            }

            try
            {
                return ReadChanges() != _current;
            }
            catch (InvalidDataException) { return true; }
        }
    }

    public WorldAsset ReadChanges()
    {
        WorldAsset current = _current ?? throw new InvalidOperationException("Select an authored world.");
        if (_terrain.SelectedIndex < 0 || _terrain.SelectedIndex >= _terrains.Length)
        {
            throw new InvalidDataException("Select the terrain bound to this world.");
        }

        return current with
        {
            Name = _name.Text ?? "",
            MapId = WorldInput.ReadMapId(_mapId.Text),
            TerrainId = _terrains[_terrain.SelectedIndex].Id,
            Flags = WorldInput.ReadFlags(_flags.Text)
        };
    }

    public void Accept(WorldAsset world) => _current = world;

    public void Load(WorldAsset world, ProjectDocument document)
    {
        _loading = true;
        try
        {
            _current = world;
            _terrains = [.. document.Terrains.OrderBy(terrain => terrain.MapDataId)];
            _terrain.ItemsSource = _terrains.Select(terrain => $"{terrain.Name} (MapDataId {terrain.MapDataId})").ToArray();
            _terrain.SelectedIndex = Array.FindIndex(_terrains, terrain => terrain.Id == world.TerrainId);
            _mapId.Text = world.MapId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _name.Text = world.Name;
            _flags.Text = $"0x{world.Flags:X16}";
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
}
