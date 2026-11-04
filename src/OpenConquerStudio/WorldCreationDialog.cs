using Avalonia;
using Avalonia.Controls;
using OpenConquerStudio.Authoring;

namespace OpenConquerStudio;

internal sealed record WorldCreation(string Name, uint MapId, Guid TerrainId, ulong Flags);

internal sealed class WorldCreationDialog : Window
{
    private readonly TextBox _name = new() { Name = "NewWorldName", Text = "World" };
    private readonly TextBox _mapId = new() { Name = "NewWorldMapId", Watermark = "Required world MapId" };
    private readonly TextBox _flags = new() { Name = "NewWorldFlags", Watermark = "Required flags: decimal or 0x hex" };
    private readonly ComboBox _terrain = new() { Name = "NewWorldTerrain" };
    private readonly TextBlock _error = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TerrainAsset[] _terrains;

    public WorldCreationDialog(IReadOnlyList<TerrainAsset> terrains)
    {
        _terrains = [.. terrains.OrderBy(terrain => terrain.MapDataId)];
        Title = "New world";
        Width = 420;
        Height = 410;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _terrain.ItemsSource = _terrains.Select(terrain => $"{terrain.Name} (MapDataId {terrain.MapDataId})").ToArray();
        StackPanel fields = new()
        {
            Margin = new Thickness(20),
            Spacing = 8
        };
        Add(fields, "World name", _name);
        Add(fields, "World MapId (not MapDataId)", _mapId);
        Add(fields, "Terrain resource", _terrain);
        Add(fields, "64-bit world flags (explicit value)", _flags);
        fields.Children.Add(_error);
        Button create = new()
        {
            Content = "Create world",
            Name = "CreateWorld",
            IsDefault = true
        };
        create.Click += (_, _) => Submit();
        Button cancel = new()
        {
            Content = "Cancel",
            IsCancel = true
        };
        cancel.Click += (_, _) => Close();
        fields.Children.Add(new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            Children = { create, cancel }
        });
        Content = new ScrollViewer { Content = fields };
    }

    private void Submit()
    {
        try
        {
            string name = _name.Text ?? "";
            ProjectDocument.ValidateName(name);
            if (_terrain.SelectedIndex < 0 || _terrain.SelectedIndex >= _terrains.Length)
            {
                throw new InvalidDataException("Select an existing terrain resource.");
            }

            Close(new WorldCreation(name, WorldInput.ReadMapId(_mapId.Text),
                _terrains[_terrain.SelectedIndex].Id, WorldInput.ReadFlags(_flags.Text)));
        }
        catch (InvalidDataException exception) { _error.Text = exception.Message; }
    }

    private static void Add(StackPanel panel, string label, Control input)
    {
        panel.Children.Add(new TextBlock { Text = label });
        panel.Children.Add(input);
    }
}
