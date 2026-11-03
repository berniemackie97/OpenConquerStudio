using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using OpenConquerStudio.Authoring;

namespace OpenConquerStudio;

internal sealed record TerrainCreation(string Name, uint MapDataId, int Width, int Height, TerrainCell Fill);

internal sealed class TerrainCreationDialog : Window
{
    private readonly TextBox _name = new()
    {
        Text = "Terrain",
        Name = "NewTerrainName"
    };
    private readonly NumericUpDown _mapDataId = Number("NewTerrainMapDataId", 1, uint.MaxValue, 1);
    private readonly NumericUpDown _width = Number("NewTerrainWidth", 1, 1024, 16);
    private readonly NumericUpDown _height = Number("NewTerrainHeight", 1, 1024, 16);
    private readonly NumericUpDown _surface = Number("InitialSurface", 0, ushort.MaxValue, 0);
    private readonly NumericUpDown _passability = Number("InitialPassability", 0, ushort.MaxValue, 0);
    private readonly NumericUpDown _elevation = Number("InitialElevation", short.MinValue, short.MaxValue, 0);
    private readonly TextBlock _error = new()
    {
        TextWrapping = Avalonia.Media.TextWrapping.Wrap
    };

    public TerrainCreationDialog()
    {
        Title = "New terrain";
        Width = 400;
        Height = 570;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        StackPanel fields = new()
        {
            Margin = new Thickness(20),
            Spacing = 7
        };
        AddField(fields, "Terrain name", _name);
        AddField(fields, "Unique MapDataId", _mapDataId);
        AddField(fields, "Width", _width);
        AddField(fields, "Height", _height);
        AddField(fields, "Initial surface ID", _surface);
        AddField(fields, "Initial passability flag (raw)", _passability);
        AddField(fields, "Initial elevation", _elevation);
        fields.Children.Add(_error);
        Button create = new()
        {
            Content = "Create terrain",
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
            Orientation = Orientation.Horizontal,
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
            int width = EditorInput.Integer(_width, "terrain width");
            int height = EditorInput.Integer(_height, "terrain height");
            if ((long)width * height > ProjectDocument.MaximumTotalTerrainCells)
            {
                throw new InvalidDataException("The terrain exceeds the current authored cell budget.");
            }
            TerrainCell fill = new((ushort)EditorInput.Integer(_surface, "surface"),
                (ushort)EditorInput.Integer(_passability, "passability"),
                (short)EditorInput.Integer(_elevation, "elevation"));
            Close(new TerrainCreation(name, EditorInput.UnsignedInteger(_mapDataId, "map data ID"), width, height, fill));
        }
        catch (InvalidDataException error) { _error.Text = error.Message; }
    }

    private static void AddField(StackPanel panel, string label, Control input)
    {
        panel.Children.Add(new TextBlock { Text = label });
        panel.Children.Add(input);
    }

    private static NumericUpDown Number(string name, decimal minimum, decimal maximum, decimal value) => new()
    {
        Name = name,
        Minimum = minimum,
        Maximum = maximum,
        Value = value,
        Increment = 1,
        FormatString = "0",
        ParsingNumberStyle = NumberStyles.Integer
    };
}
