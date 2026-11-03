using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.ConquerImport;

namespace OpenConquerStudio;

internal static class StudioDialogs
{
    public static async Task<string?> PickFolder(Window owner, string title)
    {
        IReadOnlyList<IStorageFolder> selected = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        if (selected.Count == 0)
        {
            return null;
        }

        return selected[0].TryGetLocalPath() ?? throw new IOException("Select a local filesystem directory.");
    }

    public static async Task<string?> ProjectName(Window owner)
    {
        Window dialog = Dialog("New project", 430, 240);
        TextBox name = new()
        {
            Text = "My game",
            Name = "NewProjectName"
        };
        TextBlock error = new()
        {
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };
        Button create = new()
        {
            Content = "Choose location",
            IsDefault = true
        };
        Button cancel = new()
        {
            Content = "Cancel",
            IsCancel = true
        };
        cancel.Click += (_, _) => dialog.Close();
        create.Click += (_, _) =>
        {
            string text = (name.Text ?? "").Trim();
            if (text.Length is 0 or > 120 || text is "." or ".." || text.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || text.Contains('\\') || text.Contains('/') || text.EndsWith('.') || text.EndsWith(' '))
            {
                error.Text = "Enter a project name that can be used as a folder name.";
                return;
            }
            dialog.Close(text);
        };
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12,
            Children = { new TextBlock { Text = "Project name" }, name,
                new TextBlock { Text = "Studio creates a new folder at the location you choose.", TextWrapping = Avalonia.Media.TextWrapping.Wrap }, error,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { create, cancel } } }
        };
        dialog.Opened += (_, _) => { name.Focus(); name.SelectAll(); };
        return await dialog.ShowDialog<string?>(owner);
    }

    public static async Task<string?> PickProject(Window owner)
    {
        IReadOnlyList<IStorageFile> selected = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Studio project",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Studio project") { Patterns = ["*.ocstudio"] }],
        });
        return selected.Count == 0 ? null : selected[0].TryGetLocalPath() ?? throw new IOException("Select a local project file.");
    }

    public static async Task<string?> PickTexture(Window owner)
    {
        IReadOnlyList<IStorageFile> selected = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import texture",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Textures") { Patterns = ["*.tga", "*.dds"] }],
        });
        if (selected.Count == 0)
        {
            return null;
        }

        return selected[0].TryGetLocalPath() ?? throw new IOException("Select a local filesystem file.");
    }

    public static async Task<string?> PickC3(Window owner, string title)
    {
        IReadOnlyList<IStorageFile> selected = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("C3 mesh or motion") { Patterns = ["*.c3", "*.C3"] }],
        });
        if (selected.Count == 0)
        {
            return null;
        }
        return selected[0].TryGetLocalPath() ?? throw new IOException("Select a local filesystem file.");
    }

    public static async Task<string> ConfirmChanges(Window owner)
    {
        Window dialog = Dialog("Unsaved changes", 410, 180);
        StackPanel choices = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        foreach (string choice in new[] { "Save", "Discard", "Cancel" })
        {
            Button button = new()
            {
                Content = choice,
                MinWidth = 90
            };
            button.Click += (_, _) => dialog.Close(choice);
            choices.Children.Add(button);
        }

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 20,
            Children = { new TextBlock { Text = "Save your project changes before continuing?" }, choices },
        };
        return await dialog.ShowDialog<string?>(owner) ?? "Cancel";
    }

    public static async Task<TextureCreation?> CreateTexture(Window owner)
    {
        Window dialog = Dialog("New texture", 340, 480);
        TextBox name = new()
        {
            Text = "Texture",
            PlaceholderText = "Name"
        };
        NumericUpDown width = new()
        {
            Value = 64,
            Minimum = 1,
            Maximum = 4096,
            Increment = 1,
            FormatString = "0"
        };
        NumericUpDown height = new()
        {
            Value = 64,
            Minimum = 1,
            Maximum = 4096,
            Increment = 1,
            FormatString = "0"
        };
        TextBox color = new()
        {
            Text = "4080C0",
            PlaceholderText = "RRGGBB"
        };
        TextBlock error = new()
        {
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };
        Button create = new()
        {
            Content = "Create",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        create.Click += (_, _) =>
        {
            string text = color.Text ?? "";
            if (text.Length != 6 || !uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb) || string.IsNullOrWhiteSpace(name.Text) || width.Value != decimal.Truncate(width.Value ?? 0) || height.Value != decimal.Truncate(height.Value ?? 0))
            {
                error.Text = "Enter a name, whole-number dimensions, and a six-digit RGB color.";
                return;
            }

            try
            {
                dialog.Close(new TextureCreation(name.Text, EditorInput.Integer(width, "width"), EditorInput.Integer(height, "height"), (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
            }
            catch (InvalidDataException exception) { error.Text = exception.Message; }
        };
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 8,
            Children = { new TextBlock { Text = "Name" }, name, new TextBlock { Text = "Width / height" }, width, height,
                new TextBlock { Text = "Color (RRGGBB)" }, color, error, create },
        };
        return await dialog.ShowDialog<TextureCreation?>(owner);
    }

    public static async Task<TerrainCreation?> CreateTerrain(Window owner)
    {
        Window dialog = Dialog("New terrain", 400, 570);
        TextBox name = new()
        {
            Text = "Terrain",
            Name = "NewTerrainName"
        };
        NumericUpDown mapDataId = new()
        {
            Minimum = 1,
            Maximum = uint.MaxValue,
            Value = 1,
            FormatString = "0",
            ParsingNumberStyle = NumberStyles.Integer,
            Name = "NewTerrainMapDataId"
        };
        NumericUpDown width = new()
        {
            Minimum = 1,
            Maximum = 1024,
            Value = 16,
            FormatString = "0",
            ParsingNumberStyle = NumberStyles.Integer,
            Name = "NewTerrainWidth"
        };
        NumericUpDown height = new()
        {
            Minimum = 1,
            Maximum = 1024,
            Value = 16,
            FormatString = "0",
            ParsingNumberStyle = NumberStyles.Integer,
            Name = "NewTerrainHeight"
        };
        NumericUpDown surface = new()
        {
            Minimum = 0,
            Maximum = ushort.MaxValue,
            Value = 0,
            FormatString = "0",
            ParsingNumberStyle = NumberStyles.Integer
        };
        NumericUpDown passability = new()
        {
            Minimum = 0,
            Maximum = ushort.MaxValue,
            Value = 0,
            FormatString = "0",
            ParsingNumberStyle = NumberStyles.Integer
        };
        NumericUpDown elevation = new()
        {
            Minimum = short.MinValue,
            Maximum = short.MaxValue,
            Value = 0,
            FormatString = "0",
            ParsingNumberStyle = NumberStyles.Integer
        };
        TextBlock error = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        Button create = new()
        {
            Content = "Create terrain",
            IsDefault = true
        };
        Button cancel = new()
        {
            Content = "Cancel",
            IsCancel = true
        };
        cancel.Click += (_, _) => dialog.Close();
        create.Click += (_, _) =>
        {
            try
            {
                string terrainName = name.Text ?? "";
                ProjectDocument.ValidateName(terrainName);
                int x = EditorInput.Integer(width, "terrain width");
                int y = EditorInput.Integer(height, "terrain height");
                if ((long)x * y > ProjectDocument.MaximumTotalTerrainCells)
                {
                    throw new InvalidDataException("The terrain exceeds the current authored cell budget.");
                }
                dialog.Close(new TerrainCreation(terrainName, EditorInput.UnsignedInteger(mapDataId, "map data ID"), x, y,
                    new TerrainCell((ushort)EditorInput.Integer(surface, "surface"), (ushort)EditorInput.Integer(passability, "passability"),
                        (short)EditorInput.Integer(elevation, "elevation"))));
            }
            catch (InvalidDataException exception) { error.Text = exception.Message; }
        };
        StackPanel fields = new()
        {
            Margin = new Thickness(20),
            Spacing = 7
        };
        foreach ((string label, Control field) in new (string, Control)[]
        {
            ("Terrain name", name), ("Unique MapDataId", mapDataId), ("Width", width), ("Height", height),
            ("Initial surface ID", surface), ("Initial passability flag (raw)", passability), ("Initial elevation", elevation)
        })
        {
            fields.Children.Add(new TextBlock { Text = label });
            fields.Children.Add(field);
        }
        fields.Children.Add(error);
        fields.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { create, cancel } });
        dialog.Content = new ScrollViewer { Content = fields };
        return await dialog.ShowDialog<TerrainCreation?>(owner);
    }

    public static async Task<string?> PickArtwork(Window owner)
    {
        Window dialog = Dialog("Import client artwork", 460, 420);
        ListBox list = new()
        {
            ItemsSource = ConquerArtworkImporter.Sources.Select(source => source.Name).ToArray(),
            SelectedIndex = 0
        };
        Button import = new()
        {
            Content = "Import selected image",
            HorizontalAlignment = HorizontalAlignment.Right,
            IsDefault = true
        };
        import.Click += (_, _) =>
        {
            if (list.SelectedIndex >= 0)
            {
                dialog.Close(ConquerArtworkImporter.Sources[list.SelectedIndex].Key);
            }
        };
        Grid content = new()
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            RowSpacing = 12,
            Margin = new Thickness(20)
        };
        content.Children.Add(new TextBlock { Text = "Import an image into the project. The source stays unchanged.", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        Grid.SetRow(list, 1);
        content.Children.Add(list);
        Grid.SetRow(import, 2);
        content.Children.Add(import);
        dialog.Content = content;
        return await dialog.ShowDialog<string?>(owner);
    }

    private static Window Dialog(string title, int width, int height) => new()
    {
        Title = title,
        Width = width,
        Height = height,
        CanResize = false,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
    };
}

internal sealed record TextureCreation(string Name, int Width, int Height, byte Red, byte Green, byte Blue);
internal sealed record TerrainCreation(string Name, uint MapDataId, int Width, int Height, TerrainCell Fill);
