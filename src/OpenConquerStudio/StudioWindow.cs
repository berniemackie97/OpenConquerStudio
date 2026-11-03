using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.ConquerImport;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio;

[SuppressMessage("Design", "CA1001", Justification = "Avalonia owns the window lifetime; Closed releases the preview and project after pending operations finish.")]
public sealed class StudioWindow : Window
{
    private readonly WrapPanel _commands = new() { Orientation = Orientation.Horizontal, ItemSpacing = 8, LineSpacing = 8, Margin = new Thickness(12) };
    private readonly ListBox _assets = new() { Name = "AssetBrowser" };
    private readonly TextBox _projectName = new() { PlaceholderText = "Project name", Name = "ProjectName" };
    private readonly TextBox _assetName = new() { PlaceholderText = "Asset name", Name = "AssetName" };
    private readonly CheckBox _flip = new() { Content = "Flip horizontally", Name = "FlipHorizontal" };
    private readonly ComboBox _rotation = new() { ItemsSource = new[] { "0°", "90° clockwise", "180°", "270° clockwise" }, SelectedIndex = 0, Name = "Rotation" };
    private readonly TextBlock _metadata = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _output = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90, Name = "Diagnostics" };
    private readonly AssetPreview _preview = new();
    private readonly TextBlock _projectStatus = new() { Margin = new Thickness(12, 0), TextWrapping = TextWrapping.Wrap };
    private readonly List<Control> _projectCommands = [];
    private readonly List<Control> _assetCommands = [];
    private readonly Button _cancel = new() { Content = "Cancel operation", IsEnabled = false };

    private readonly TextBlock _dependencies = new() { TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _textureInspector = new() { Spacing = 10 };
    private readonly AtlasEditor _atlasEditor;
    private readonly AnimationEditor _animationEditor;
    private readonly Button _undo = new() { Content = "Undo", Name = "Undo" };
    private readonly Button _redo = new() { Content = "Redo", Name = "Redo" };
    private readonly TabControl _assetTabs;
    private readonly ClientWorkspace _clientWorkspace;
    private ClientContentPanel ClientContent => _clientWorkspace.Panel;
    private StudioProject? _project;
    private CancellationTokenSource? _cancellation;
    private Task? _operation;
    private bool _busy;
    private bool _closing;
    private bool _refreshing;
    private bool _allowClose;

    public StudioWindow(string? projectRoot = null)
    {
        Title = "OpenConquerStudio";
        Width = 1280;
        Height = 760;
        MinWidth = 1150;
        MinHeight = 600;
        _clientWorkspace = new ClientWorkspace(this, _preview);
        _atlasEditor = new AtlasEditor(asset => ExecuteAsync(async token =>
        {
            _project!.EditAtlas(asset);
            await UpdatePreview(asset.Id, token);
        }), spriteId => { if (_assets.SelectedItem is ResourceInfo row) { _ = ExecuteAsync(token => UpdatePreview(row.Id, token)); } });
        _animationEditor = new AnimationEditor(asset => ExecuteAsync(async token =>
        {
            _project!.EditAnimation(asset);
            await UpdatePreview(asset.Id, token);
        }), _preview.SelectFrame);
        _undo.Click += async (_, _) => await RestoreHistory(undo: true);
        _redo.Click += async (_, _) => await RestoreHistory(undo: false);
        KeyModifiers modifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        _undo.HotKey = new KeyGesture(Key.Z, modifier);
        _redo.HotKey = new KeyGesture(Key.Z, modifier | KeyModifiers.Shift);
        AddCommand("New project", NewProject);
        AddCommand("Open project", OpenProject);
        AddCommand("Open client project", OpenClientProject);
        AddCommand("Client content folder", _clientWorkspace.ChangeContentFolder, requiresProject: true);
        AddCommand("Import texture", ImportTexture, requiresProject: true);
        AddCommand("Client artwork", ImportArtwork, requiresProject: true);
        AddCommand("New texture", CreateTexture, requiresProject: true);
        AddCommand("New atlas", CreateAtlas, requiresProject: true);
        AddCommand("New animation", CreateAnimation, requiresProject: true);
        AddCommand("Delete resource", DeleteResource, requiresProject: true);
        _commands.Children.Add(_undo);
        _commands.Children.Add(_redo);
        AddCommand("Save", SaveProject, requiresProject: true);
        AddCommand("Save and build", Build, requiresProject: true);
        _cancel.Click += (_, _) => _cancellation?.Cancel();
        _commands.Children.Add(_cancel);

        Button rename = new()
        {
            Content = "Rename project"
        };
        rename.Click += async (_, _) => await ExecuteAsync(_ => { _project!.Rename(_projectName.Text ?? ""); return Task.CompletedTask; });
        _projectCommands.Add(rename);
        Button apply = new()
        {
            Content = "Apply asset edits",
            Name = "ApplyAssetEdits"
        };
        apply.Click += async (_, _) => await ExecuteAsync(async token =>
        {
            if (_assets.SelectedItem is ResourceInfo row)
            {
                _project!.EditTexture(row.Id, _assetName.Text ?? "", _flip.IsChecked == true, _rotation.SelectedIndex);
                await UpdatePreview(row.Id, token);
            }
        });
        _assetCommands.Add(apply);
        Button replace = new()
        {
            Content = "Replace source image"
        };
        replace.Click += async (_, _) => await ExecuteAsync(async token =>
        {
            if (_assets.SelectedItem is ResourceInfo row)
            {
                string? path = await StudioDialogs.PickTexture(this);
                if (path is not null)
                {
                    await Task.Run(() => _project!.ReplaceTexture(row.Id, path, token), token);
                    await UpdatePreview(row.Id, token);
                    _output.Text = "Source image replaced. Asset identity and edit settings were preserved.";
                }
            }
        });
        _assetCommands.Add(replace);
        _assetCommands.Add(_assetName);
        _assetCommands.Add(_flip);
        _assetCommands.Add(_rotation);
        _projectCommands.Add(_projectName);
        _assets.SelectionChanged += async (_, _) =>
        {
            if (!_busy && !_refreshing && _assets.SelectedItem is ResourceInfo row)
            {
                await ExecuteAsync(token => UpdatePreview(row.Id, token));
            }
        };

        Grid body = new()
        {
            ColumnDefinitions = new ColumnDefinitions("300,5,*,5,275"),
            Margin = new Thickness(12)
        };
        Grid browser = new()
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*"),
            RowSpacing = 8
        };
        _assetTabs = new()
        {
            Name = "ProjectContentTabs",
            ItemsSource = new TabItem[]
            {
                new() { Header = "Authored", Content = _assets },
                new() { Header = "Client content", Content = ClientContent },
            },
        };
        Control[] browserControls = [Heading("Project"), _projectName, rename, Heading("Assets"), _assetTabs];
        for (int row = 0; row < browserControls.Length; row++)
        {
            Grid.SetRow(browserControls[row], row);
            browser.Children.Add(browserControls[row]);
        }
        Grid.SetColumn(browser, 0);
        body.Children.Add(browser);
        body.Children.Add(new GridSplitter { Width = 5, HorizontalAlignment = HorizontalAlignment.Stretch, [Grid.ColumnProperty] = 1 });
        Grid.SetColumn(_preview, 2);
        body.Children.Add(_preview);
        body.Children.Add(new GridSplitter { Width = 5, HorizontalAlignment = HorizontalAlignment.Stretch, [Grid.ColumnProperty] = 3 });
        _textureInspector.Children.Add(Heading("Texture"));
        foreach (Control control in new Control[] { _metadata, _assetName, _flip, _rotation, apply, replace })
        {
            _textureInspector.Children.Add(control);
        }
        StackPanel inspector = new()
        {
            Spacing = 10,
            Margin = new Thickness(12, 0, 0, 0),
            [Grid.ColumnProperty] = 4,
            Children = { _textureInspector, _atlasEditor, _animationEditor, ClientContent.Inspector, Heading("Dependencies"), _dependencies }
        };
        body.Children.Add(new ScrollViewer { Content = inspector, [Grid.ColumnProperty] = 4, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        Grid root = new()
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,120")
        };
        root.Children.Add(_commands);
        Grid.SetRow(_projectStatus, 1);
        root.Children.Add(_projectStatus);
        Grid.SetRow(body, 2);
        root.Children.Add(body);
        Border output = new()
        {
            Margin = new Thickness(12, 0, 12, 12),
            Child = _output,
            [Grid.RowProperty] = 3
        };
        root.Children.Add(output);
        Content = root;
        _projectName.SetValue(AutomationProperties.NameProperty, "Project name");
        _assetName.SetValue(AutomationProperties.NameProperty, "Asset name");
        _rotation.SetValue(AutomationProperties.NameProperty, "Clockwise rotation");
        if (projectRoot is not null)
        {
            Opened += async (_, _) => await ExecuteAsync(token => LoadProject(projectRoot, token));
        }
        Closing += HandleClosing;
        Closed += (_, _) => { _project?.Dispose(); _preview.Dispose(); };
        ClearPreview();
        Refresh();
        _output.Text = "Create an empty project, then import a TGA/DDS texture or create a new texture.";
    }

    private static TextBlock Heading(string text) => new() { Text = text, FontWeight = FontWeight.SemiBold, FontSize = 16 };

    private void AddCommand(string text, Func<CancellationToken, Task> action, bool requiresProject = false)
    {
        Button button = new()
        {
            Content = text
        };
        button.Click += async (_, _) => await ExecuteAsync(action);
        if (text == "Save")
        {
            button.HotKey = new KeyGesture(Key.S, OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control);
        }
        _commands.Children.Add(button);
        if (requiresProject)
        {
            _projectCommands.Add(button);
        }
    }

    internal StudioProject Project => _project ?? throw new InvalidOperationException("Open a project first.");
    internal ResourceInfo? SelectedAsset => _assets.SelectedItem as ResourceInfo;
    internal string Diagnostic
    {
        set => _output.Text = value;
    }

    internal void ShowSourceInspector()
    {
        _textureInspector.IsVisible = false;
        _atlasEditor.IsVisible = false;
        _animationEditor.IsVisible = false;
        ClientContent.Inspector.IsVisible = true;
    }

    internal Task PendingOperation => _operation ?? Task.CompletedTask;

    internal Task ExecuteAsync(Func<CancellationToken, Task> action)
    {
        if (_busy)
        {
            return Task.CompletedTask;
        }

        _operation = ExecuteCore(action);
        return _operation;
    }

    private async Task ExecuteCore(Func<CancellationToken, Task> action)
    {
        _busy = true;
        using CancellationTokenSource cancellation = new();
        _cancellation = cancellation;
        RefreshEnabled();
        try
        {
            await action(cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            _output.Text = "Operation canceled. Any edits remain available to save.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
        {
            _output.Text = exception.Message;
        }
        finally
        {
            _cancellation = null;
            _busy = false;
            Refresh();
        }
    }

    private async Task<bool> CanReplaceProject(CancellationToken token)
    {
        if (_project?.IsModified != true)
        {
            return true;
        }

        string choice = await StudioDialogs.ConfirmChanges(this);
        if (choice == "Save")
        {
            await Task.Run(() => _project.Save(token), token);
        }

        return choice != "Cancel";
    }

    private async Task NewProject(CancellationToken token)
    {
        if (!await CanReplaceProject(token))
        {
            return;
        }

        string? root = await StudioDialogs.PickFolder(this, "Select an empty directory for the project");
        if (root is not null)
        {
            StudioProject candidate = await Task.Run(() => StudioProject.Create(root, Path.GetFileName(root)), token);
            ReplaceProject(candidate);
        }
    }

    private async Task OpenProject(CancellationToken token)
    {
        if (!await CanReplaceProject(token))
        {
            return;
        }

        string? root = await StudioDialogs.PickFolder(this, "Open project directory");
        if (root is not null && root != _project?.Root)
        {
            await LoadProject(root, token);
        }
    }

    internal async Task LoadProject(string root, CancellationToken token)
    {
        (StudioProject candidate, ClientProjectConnection? connection, ImmutableArray<ClientAssetEntry> inventory, string? warning) = await Task.Run(() =>
        {
            bool clientRepository = ClientProjectConnection.IsClientRepository(root);
            string workspace = clientRepository ? Path.Combine(root, ".studio", "authoring") : root;
            StudioProject project;
            ClientProjectConnection? attached = null;
            if (clientRepository && !File.Exists(Path.Combine(workspace, "project.ocstudio")))
            {
                (project, attached) = ClientProjectConnection.OpenRepository(root, token);
            }
            else
            {
                project = StudioProject.Open(workspace, token);
            }

            try
            {
                attached ??= ClientProjectConnection.OpenAttached(project.Root);
                if (clientRepository && attached is null)
                {
                    attached = ClientProjectConnection.Attach(project, root, Path.Combine(root, "content", "retail-5517", "payload"), token);
                }
                return (project, attached, attached?.Inventory(token) ?? [], (string?)null);
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException)
            {
                // A missing local source connection must not make valid authored assets inaccessible.
                return (project, (ClientProjectConnection?)null, ImmutableArray<ClientAssetEntry>.Empty,
                    "Client connection unavailable: " + error.Message + " Use Client content folder to reconnect. Authored assets remain available.");
            }
            catch
            {
                project.Dispose();
                throw;
            }
        }, token);
        ReplaceProject(candidate);
        if (connection is not null)
        {
            _clientWorkspace.Attach(connection, inventory);
            _assetTabs.SelectedIndex = 1;
            _output.Text = $"Connected to {connection.Repository}\nSource content: {connection.ContentRoot}\n{inventory.Length} client source entries. Select Client content to browse maps, ANI sections, images, and puzzles.";
        }
        else if (warning is not null)
        {
            _output.Text = warning;
        }
    }

    private void ReplaceProject(StudioProject candidate)
    {
        _project?.Dispose();
        _project = candidate;
        _clientWorkspace.Reset();
        _assetTabs.SelectedIndex = 0;
        ClearPreview();
        _metadata.Text = "Select a texture to inspect its properties.";
        _output.Text = "Project opened: " + candidate.Root;
    }

    private async Task OpenClientProject(CancellationToken token)
    {
        if (!await CanReplaceProject(token))
        {
            return;
        }

        string? repository = await StudioDialogs.PickFolder(this, "Open the OpenConquerClient repository");
        if (repository is not null)
        {
            await LoadProject(repository, token);
        }
    }

    private async Task ImportTexture(CancellationToken token)
    {
        string? path = await StudioDialogs.PickTexture(this);
        if (path is not null)
        {
            Guid id = await Task.Run(() => _project!.ImportTexture(path, Path.GetFileNameWithoutExtension(path), token), token);
            await SelectAsset(id, token);
            _output.Text = "Texture imported. Save the project to persist it.";
        }
    }

    private async Task ImportArtwork(CancellationToken token)
    {
        string? key = await StudioDialogs.PickArtwork(this);
        if (key is not null)
        {
            Guid id = await Task.Run(() => ConquerArtworkImporter.Import(_project!, key, token), token);
            await SelectAsset(id, token);
            _output.Text = "Client artwork imported. Save the project to persist your editable copy.";
        }
    }

    private async Task CreateTexture(CancellationToken token)
    {
        TextureCreation? creation = await StudioDialogs.CreateTexture(this);
        if (creation is not null)
        {
            Guid id = await Task.Run(() => _project!.CreateTexture(creation.Name, creation.Width, creation.Height, creation.Red, creation.Green, creation.Blue), token);
            await SelectAsset(id, token);
            _output.Text = "Texture created. Save the project to persist it.";
        }
    }

    internal async Task SelectAsset(Guid id, CancellationToken token)
    {
        _assetTabs.SelectedIndex = 0;
        RefreshAssets(id);
        await UpdatePreview(id, token);
    }

    private async Task UpdatePreview(Guid id, CancellationToken token)
    {
        ProjectDocument document = _project!.Document;
        ClientContent.Inspector.IsVisible = false;
        ResourceInfo info = document.Resources.Single(a => a.Id == id);
        _preview.Pause();
        _textureInspector.IsVisible = info.Kind == "texture";
        _atlasEditor.IsVisible = info.Kind == "atlas";
        _animationEditor.IsVisible = info.Kind == "animation";
        _dependencies.Text = "Uses: " + string.Join(", ", document.Dependencies(id).Select(dependency => document.Resources.Single(a => a.Id == dependency).Name)) +
            "\nUsed by: " + string.Join(", ", document.Dependents(id).Select(a => a.Name));
        if (info.Kind == "atlas")
        {
            _atlasEditor.Load(document.Atlases.Single(a => a.Id == id), document);
        }
        else if (info.Kind == "animation")
        {
            _animationEditor.Load(document.Animations.Single(a => a.Id == id), document);
        }

        Guid selectedSprite = _atlasEditor.SelectedSprite;
        ImmutableArray<ViewportSprite> frames = await Task.Run(() => MakePreview(info, selectedSprite, token), token);
        AnimationAsset? animation = document.Animations.FirstOrDefault(a => a.Id == id);
        _preview.Load(frames, animation is null ? null : new SpriteAnimation(animation.Id, animation.TicksPerSecond, animation.Repeat, animation.Frames));
        if (animation is not null)
        {
            _preview.SelectFrame(_animationEditor.SelectedFrame);
        }
        TextureData texture = frames[0].Texture;
        if (info.Kind == "texture")
        {
            TextureAsset asset = document.Assets.Single(a => a.Id == id);
            _assetName.Text = asset.Name;
            _flip.IsChecked = asset.FlipHorizontal;
            _rotation.SelectedIndex = asset.ClockwiseQuarterTurns;
            _metadata.Text = $"{texture.Width} × {texture.Height} RGBA8\nID: {asset.Id}\nSource: {asset.SourceFormat}";
        }
    }

    private ImmutableArray<ViewportSprite> MakePreview(ResourceInfo info, Guid selectedSprite, CancellationToken token)
    {
        ProjectDocument document = _project!.Document;
        Dictionary<Guid, TextureData> textures = [];
        TextureData GetTexture(Guid id)
        {
            if (!textures.TryGetValue(id, out TextureData? texture))
            {
                texture = _project.Preview(id, token);
                textures.Add(id, texture);
            }

            return texture;
        }

        ViewportSprite GetSprite(Guid atlasId, Guid spriteId)
        {
            AtlasAsset atlas = document.Atlases.Single(a => a.Id == atlasId);
            return new ViewportSprite(atlas.TextureId, GetTexture(atlas.TextureId), atlas.Sprites.Single(s => s.Id == spriteId));
        }

        if (info.Kind == "texture")
        {
            TextureData texture = GetTexture(info.Id);
            return [new ViewportSprite(info.Id, texture, new SpriteRegion(info.Id, info.Name, 0, 0, texture.Width, texture.Height, texture.Width / 2, texture.Height / 2))];
        }

        if (info.Kind == "atlas")
        {
            return [GetSprite(info.Id, selectedSprite)];
        }

        return [.. document.Animations.Single(a => a.Id == info.Id).Frames.Select(f => GetSprite(f.AtlasId, f.SpriteId))];
    }

    private void ClearPreview()
    {
        _preview.Clear();
        _textureInspector.IsVisible = false;
        _atlasEditor.IsVisible = false;
        _animationEditor.IsVisible = false;
        _dependencies.Text = "Select a resource.";
    }

    private async Task RestoreHistory(bool undo)
    {
        await ExecuteAsync(async token =>
        {
            Guid? id = (_assets.SelectedItem as ResourceInfo)?.Id;
            if (undo)
            {
                _project!.Undo();
            }
            else
            {
                _project!.Redo();
            }
            RefreshAssets(id);
            if (id is Guid selected && _project.Document.Resources.Any(a => a.Id == selected))
            {
                await UpdatePreview(selected, token);
            }
            else
            {
                ClearPreview();
            }
        });
    }

    private async Task CreateAtlas(CancellationToken token)
    {
        if (_assets.SelectedItem is not ResourceInfo { Kind: "texture" } row)
        {
            throw new InvalidOperationException("Select a texture, then create an atlas from it.");
        }

        Guid id = _project!.CreateAtlas(row.Name + " atlas", row.Id);
        await SelectAsset(id, token);
    }

    private async Task CreateAnimation(CancellationToken token)
    {
        if (_assets.SelectedItem is not ResourceInfo { Kind: "atlas" } row)
        {
            throw new InvalidOperationException("Select an atlas, then create an animation from its ordered sprites.");
        }

        Guid id = _project!.CreateAnimation(row.Name + " animation", row.Id);
        await SelectAsset(id, token);
    }

    private Task DeleteResource(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_assets.SelectedItem is ResourceInfo row)
        {
            _project!.Delete(row.Id);
            ClearPreview();
        }

        return Task.CompletedTask;
    }

    private async Task SaveProject(CancellationToken token)
    {
        await Task.Run(() => _project!.Save(token), token);
        _output.Text = "Project saved.";
    }

    private async Task Build(CancellationToken token)
    {
        string? parent = await StudioDialogs.PickFolder(this, "Select a directory to hold the new build");
        if (parent is null)
        {
            return;
        }

        string destination = Path.Combine(parent, "content-" + Guid.NewGuid().ToString("N"));
        ContentBuild build = await Task.Run(() => { _project!.Save(token); return StudioCompiler.Compile(_project, destination, token); }, token);
        _output.Text = $"Compiled and validated {build.Textures.Length} textures and {build.Resources.Length} sprite resources.\n{destination}";
    }

    private void Refresh()
    {
        _projectName.Text = _project?.Document.Name ?? "";
        _projectStatus.Text = _project is null ? "No project open" : $"{_project.Root}  |  {_project.Document.Id}  {(_project.IsModified ? "• Unsaved changes" : "• Saved")}";
        Title = (_project?.IsModified == true ? "* " : "") + "OpenConquerStudio" + (_project is null ? "" : " — " + _project.Document.Name);
        RefreshAssets((_assets.SelectedItem as ResourceInfo)?.Id);
        RefreshEnabled();
    }

    private void RefreshAssets(Guid? selected)
    {
        ResourceInfo[] rows = _project?.Document.Resources.ToArray() ?? [];
        _refreshing = true;
        try
        {
            _assets.ItemsSource = rows;
            _assets.SelectedItem = rows.FirstOrDefault(a => a.Id == selected);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void RefreshEnabled()
    {
        foreach (Control command in _commands.Children)
        {
            command.IsEnabled = !_busy;
        }

        foreach (Control command in _projectCommands)
        {
            command.IsEnabled = !_busy && _project is not null;
        }

        foreach (Control command in _assetCommands)
        {
            command.IsEnabled = !_busy && _assets.SelectedItem is ResourceInfo { Kind: "texture" };
        }

        _undo.IsEnabled = !_busy && _project?.CanUndo == true;
        _redo.IsEnabled = !_busy && _project?.CanRedo == true;
        _atlasEditor.IsEnabled = !_busy;
        _animationEditor.IsEnabled = !_busy;
        ClientContent.IsEnabled = !_busy;
        _assets.IsEnabled = !_busy;
        _cancel.IsEnabled = _busy;
    }

    private async void HandleClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        if (_closing)
        {
            return;
        }

        _closing = true;
        try
        {
            _cancellation?.Cancel();
            if (_operation is not null)
            {
                await _operation;
            }

            bool close = false;
            await ExecuteAsync(async token => close = await CanReplaceProject(token));
            if (close)
            {
                _allowClose = true;
                Close();
            }
        }
        finally
        {
            _closing = false;
        }
    }

}
