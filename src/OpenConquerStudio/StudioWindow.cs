using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using OpenConquer.Content.Mesh;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.ConquerImport;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio;

[SuppressMessage("Design", "CA1001", Justification = "Avalonia owns the window lifetime; Closed releases the preview and project after pending operations finish.")]
public sealed partial class StudioWindow : Window
{
    private readonly Menu _menu = new() { Name = "MainMenu" };
    private readonly MenuItem _fileMenu = new() { Header = "File" };
    private readonly MenuItem _editMenu = new() { Header = "Edit" };
    private readonly MenuItem _assetsMenu = new() { Header = "Assets" };
    private readonly MenuItem _buildMenu = new() { Header = "Build" };
    private readonly List<(Control Control, string Kind)> _contextCommands = [];
    private readonly MenuItem _undoMenu = new() { Header = "Undo" };
    private readonly MenuItem _redoMenu = new() { Header = "Redo" };
    private readonly TextBlock _notice = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12, Name = "OperationNotice" };
    private readonly Expander _activity = new() { Header = "Activity", Name = "ActivityPanel" };
    private readonly WrapPanel _commands = new() { Orientation = Orientation.Horizontal, ItemSpacing = 8, LineSpacing = 8, Margin = new Thickness(12) };
    private readonly ListBox _assets = new() { Name = "AssetBrowser" };
    private readonly TextBox _projectName = new() { PlaceholderText = "Project name", Name = "ProjectName" };
    private readonly TextBox _assetName = new() { PlaceholderText = "Asset name", Name = "AssetName" };
    private readonly CheckBox _flip = new() { Content = "Flip horizontally", Name = "FlipHorizontal" };
    private readonly ComboBox _rotation = new() { ItemsSource = new[] { "0°", "90° clockwise", "180°", "270° clockwise" }, SelectedIndex = 0, Name = "Rotation" };
    private readonly TextBlock _metadata = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _output = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90, Name = "Diagnostics" };
    private readonly TextBlock _emptyPreview = new()
    {
        Text = "Open a project to begin",
        Name = "EmptyPreview",
        FontSize = 16,
        TextWrapping = TextWrapping.Wrap,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(36),
    };
    private readonly AssetPreview _preview = new();
    private readonly TextBlock _projectStatus = new() { Margin = new Thickness(12, 0), TextWrapping = TextWrapping.Wrap };
    private readonly List<Control> _projectCommands = [];
    private readonly List<Control> _assetCommands = [];
    private readonly Button _cancel = new() { Content = "Cancel operation", IsEnabled = false };

    private readonly TextBlock _dependencies = new() { TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _textureInspector = new() { Spacing = 10 };
    private readonly AtlasEditor _atlasEditor;
    private readonly AnimationEditor _animationEditor;
    private readonly ModelEditor _modelEditor;
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
        _menu.ItemsSource = new[] { _fileMenu, _editMenu, _assetsMenu, _buildMenu };
        _undoMenu.Click += async (_, _) => await RestoreHistory(true);
        _redoMenu.Click += async (_, _) => await RestoreHistory(false);
        _editMenu.ItemsSource = new[] { _undoMenu, _redoMenu };
        Title = "OpenConquerStudio";
        Width = 1280;
        Height = 760;
        MinWidth = 1000;
        MinHeight = 600;
        _clientWorkspace = new ClientWorkspace(this, _preview);
        _atlasEditor = new AtlasEditor(readAsset => ExecuteAsync(async token =>
        {
            AtlasAsset asset = readAsset();
            _project!.EditAtlas(asset);
            await UpdatePreview(asset.Id, token);
        }), spriteId => { if (_assets.SelectedItem is ResourceInfo row) { _ = ExecuteAsync(token => UpdatePreview(row.Id, token)); } }, CommitForNavigation);
        _animationEditor = new AnimationEditor(readAsset => ExecuteAsync(async token =>
        {
            AnimationAsset asset = readAsset();
            _project!.EditAnimation(asset);
            await UpdatePreview(asset.Id, token);
        }), _preview.SelectFrame, CommitForNavigation);
        _modelEditor = new ModelEditor((name, clip, milliseconds, repeat) => ExecuteAsync(async token =>
        {
            if (SelectedAsset is not { Kind: "model" } selected)
            {
                return;
            }
            await Task.Run(() => Project.EditModel(selected.Id, name, clip, milliseconds, repeat), token);
            await UpdatePreview(selected.Id, token, clip);
        }), clip => ExecuteAsync(async token =>
        {
            if (SelectedAsset is { Kind: "model" } selected)
            {
                await UpdatePreview(selected.Id, token, clip);
            }
        }), () => ExecuteAsync(async token =>
        {
            if (SelectedAsset is not { Kind: "model" } selected)
            {
                return;
            }
            string? path = await StudioDialogs.PickC3(this, "Add a compatible C3 motion file");
            if (path is null)
            {
                return;
            }
            await Task.Run(() => Project.AddModelMotion(selected.Id, Path.GetFileNameWithoutExtension(path), path, token), token);
            int lastClip = await Task.Run(() => Project.PreviewModel(selected.Id).Definition.Clips.Length - 1, token);
            await UpdatePreview(selected.Id, token, lastClip);
        }), _preview.ModelView, PreviewModelOnMap);
        _projectName.TextChanged += (_, _) => PendingEditsChanged();
        _assetName.TextChanged += (_, _) => PendingEditsChanged();
        _flip.IsCheckedChanged += (_, _) => PendingEditsChanged();
        _rotation.SelectionChanged += (_, _) => PendingEditsChanged();
        _atlasEditor.PendingChanged += PendingEditsChanged;
        _animationEditor.PendingChanged += PendingEditsChanged;
        _modelEditor.PendingChanged += PendingEditsChanged;
        _preview.ModelFrameChanged += _modelEditor.UpdateFrame;
        _undo.Click += async (_, _) => await RestoreHistory(undo: true);
        _redo.Click += async (_, _) => await RestoreHistory(undo: false);
        KeyModifiers modifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        _undo.HotKey = new KeyGesture(Key.Z, modifier);
        _redo.HotKey = new KeyGesture(Key.Z, modifier | KeyModifiers.Shift);
        AddCommand("New project", NewProject);
        AddCommand("Open project", OpenProject);
        AddCommand("Open client project", OpenClientProject);
        AddCommand("Close project", CloseProject, requiresProject: true);
        AddCommand("Client content folder", _clientWorkspace.ChangeContentFolder, requiresProject: true);
        AddCommand("Import texture", ImportTexture, requiresProject: true);
        AddCommand("Import C3", ImportModel, requiresProject: true);
        AddCommand("Client artwork", ImportArtwork, requiresProject: true);
        AddCommand("New texture", CreateTexture, requiresProject: true);
        AddCommand("New atlas", CreateAtlas, requiresProject: true, selectionKind: "texture");
        AddCommand("New animation", CreateAnimation, requiresProject: true, selectionKind: "atlas");
        AddCommand("Delete resource", DeleteResource, requiresProject: true, selectionKind: "*");
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
        _assetTabs.SelectionChanged += async (_, e) =>
        {
            if (e.Source != _assetTabs || _busy || _refreshing)
            {
                return;
            }
            int target = _assetTabs.SelectedIndex;
            if (target == 0)
            {
                await ExecuteAsync(async token =>
                {
                    _activeWorkspaceTab = 0;
                    if (_assets.SelectedItem is ResourceInfo selected)
                    {
                        await UpdatePreview(selected.Id, token);
                    }
                    else
                    {
                        ClearPreview();
                    }
                });
            }
            else
            {
                await ExecuteAsync(_ =>
                {
                    ShowSourceInspector();
                    _preview.Clear();
                    return Task.CompletedTask;
                });
                if (_activeWorkspaceTab == 1)
                {
                    await ClientContent.PreviewSelection();
                }
            }
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
        Grid previewArea = new()
        {
            Children = { _preview, _emptyPreview },
            [Grid.ColumnProperty] = 2
        };
        body.Children.Add(previewArea);
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
            Children = { _textureInspector, _atlasEditor, _animationEditor, _modelEditor, ClientContent.Inspector, Heading("Dependencies"), _dependencies }
        };
        body.Children.Add(new ScrollViewer { Content = inspector, [Grid.ColumnProperty] = 4, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        Grid root = new()
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto")
        };
        root.Children.Add(new StackPanel { Children = { _menu, _commands } });
        Grid.SetRow(_projectStatus, 1);
        root.Children.Add(_projectStatus);
        Grid.SetRow(body, 2);
        root.Children.Add(body);
        _activity.Content = _output;
        _output.MaxHeight = 160;
        StackPanel output = new()
        {
            Margin = new Thickness(12, 0, 12, 8),
            Spacing = 4,
            Children = { _notice, _activity },
            [Grid.RowProperty] = 3
        };
        _output.TextChanged += (_, _) =>
        {
            string message = _output.Text ?? "";
            _notice.Text = message.Split('\n')[0];
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
        _output.Text = "Open a Studio project or connect your client checkout. Use Assets to import or create editable content.";
    }

    private static TextBlock Heading(string text) => new() { Text = text, FontWeight = FontWeight.SemiBold, FontSize = 16 };

    private void AddCommand(string text, Func<CancellationToken, Task> action, bool requiresProject = false, string? selectionKind = null)
    {
        MenuItem menu = text switch
        {
            "New project" or "Open project" or "Open client project" or "Close project" or "Client content folder" or "Save" => _fileMenu,
            "Save and build" => _buildMenu,
            _ => _assetsMenu,
        };
        string caption = text == "Save and build" ? "Build" : text;
        MenuItem item = new()
        {
            Header = caption
        };
        bool commit = text is not ("New project" or "Open project" or "Open client project" or "Close project");
        item.Click += async (_, _) => await ExecuteAsync(action, commit);
        menu.Items.Add(item);
        if (requiresProject)
        {
            _projectCommands.Add(item);
        }
        if (selectionKind is not null)
        {
            _contextCommands.Add((item, selectionKind));
        }
        if (text is not ("New project" or "Open project" or "Open client project" or "Save" or "Save and build"))
        {
            return;
        }
        Button button = new()
        {
            Content = caption,
            Name = text == "Save and build" ? "BuildProject" : null
        };
        button.Click += async (_, _) => await ExecuteAsync(action, commit);
        if (text == "Save")
        {
            button.HotKey = new KeyGesture(Key.S, OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control);
        }
        _commands.Children.Add(button);
        if (requiresProject)
        {
            _projectCommands.Add(button);
        }
        if (selectionKind is not null)
        {
            _contextCommands.Add((button, selectionKind));
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
        _emptyPreview.IsVisible = false;
        _preview.IsVisible = true;
        _assetTabs.SelectedIndex = 1;
        _dependencies.Text = "Client sources are read-only. Imported copies have their own authored dependencies.";
        _activeWorkspaceTab = 1;
        _textureInspector.IsVisible = false;
        _atlasEditor.IsVisible = false;
        _animationEditor.IsVisible = false;
        _modelEditor.IsVisible = false;
        ClientContent.Inspector.IsVisible = true;
    }

    internal void ClearSourcePreview()
    {
        if (_activeWorkspaceTab != 1)
        {
            return;
        }
        _preview.Clear();
        _emptyPreview.Text = "Select client content to preview.";
        _emptyPreview.IsVisible = true;
        _preview.IsVisible = false;
    }

    internal Task PendingOperation => _operation ?? Task.CompletedTask;

    internal Task ExecuteAsync(Func<CancellationToken, Task> action, bool commitEdits = true)
    {
        if (_busy)
        {
            return Task.CompletedTask;
        }

        _operation = ExecuteCore(action, commitEdits);
        return _operation;
    }

    private async Task ExecuteCore(Func<CancellationToken, Task> action, bool commitEdits)
    {
        _busy = true;
        using CancellationTokenSource cancellation = new();
        _cancellation = cancellation;
        RefreshEnabled();
        try
        {
            if (commitEdits)
            {
                await CommitPendingEdits(cancellation.Token);
            }
            await action(cancellation.Token);
            if (_previewInvalidated && _activeWorkspaceTab == 0 && _inspectedAsset is Guid pending &&
                _project?.Document.Resources.Any(r => r.Id == pending) == true)
            {
                await UpdatePreview(pending, cancellation.Token, _modelEditor.ActiveClip);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            _output.Text = "Operation canceled. Any edits remain available to save.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
        {
            _activity.IsExpanded = true;
            _output.Text = exception.Message + " Fix the field or use Undo to discard pending edits.";
            if (_inspectedAsset is Guid inspected && _project?.Document.Resources.Any(r => r.Id == inspected) == true)
            {
                _refreshing = true;
                try
                {
                    _assetTabs.SelectedIndex = _activeWorkspaceTab;
                    RefreshAssets(inspected);
                    _modelEditor.RestoreClipSelection();
                }
                finally { _refreshing = false; }
            }
        }
        finally
        {
            _cancellation = null;
            _busy = false;
            Refresh();
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

    private Task PreviewModelOnMap() => ExecuteAsync(async token =>
    {
        if (SelectedAsset is not { Kind: "model" } selected)
        {
            return;
        }
        C3ModelPreview model = await Task.Run(() =>
        {
            C3ModelData data = Project.PreviewModel(selected.Id);
            return C3ModelPreview.Create(data, data.Definition.Clips.IsEmpty ? -1 : 0);
        }, token);
        _modelEditor.Load(Project.Document.Models.Single(m => m.Id == selected.Id).Name, model);
        _clientWorkspace.PreviewOnMap(model);
    });

    private async Task ImportModel(CancellationToken token)
    {
        string? path = await StudioDialogs.PickC3(this, "Import a C3 mesh file");
        if (path is null)
        {
            return;
        }
        Guid id = await Task.Run(() => Project.ImportModelFile(path, Path.GetFileNameWithoutExtension(path), token), token);
        await SelectAsset(id, token);
    }

    private async Task RestoreHistory(bool undo)
    {
        await ExecuteAsync(async token =>
        {
            Guid? id = (_assets.SelectedItem as ResourceInfo)?.Id;
            if (HasPendingEdits)
            {
                _loadingInspector = true;
                try
                {
                    _projectName.Text = Project.Document.Name;
                }
                finally { _loadingInspector = false; }
                if (id is Guid dirty && _activeWorkspaceTab == 0)
                {
                    await UpdatePreview(dirty, token);
                }
                _output.Text = "Pending edits reverted.";
                return;
            }
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
        }, commitEdits: false);
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

    internal async Task Build(CancellationToken token)
    {
        string destination = Path.Combine(Project.Root, "Builds", "content-" + Guid.NewGuid().ToString("N")[..8]);
        ContentBuild build = await Task.Run(() => { Project.Save(token); return StudioCompiler.Compile(Project, destination, token); }, token);
        _output.Text = $"Built {build.Textures.Length} textures and {build.Resources.Length} resources.\n{destination}";
    }

    private async Task CloseProject(CancellationToken token)
    {
        if (!await CanReplaceProject(token))
        {
            return;
        }
        _project?.Dispose();
        _project = null;
        _activeWorkspaceTab = 0;
        _assetTabs.SelectedIndex = 0;
        _projectName.Text = "";
        _clientWorkspace.Reset();
        ClearPreview();
        _output.Text = "Project closed. Open a project or create a new one.";
    }

    private void Refresh()
    {
        RefreshShell();
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

        foreach ((Control control, string kind) in _contextCommands)
        {
            control.IsEnabled = !_busy && _activeWorkspaceTab == 0 && SelectedAsset is { } selected && (kind == "*" || selected.Kind == kind);
        }
        _undo.IsEnabled = !_busy && (_project?.CanUndo == true || HasPendingEdits);
        _undoMenu.IsEnabled = _undo.IsEnabled;
        _redoMenu.IsEnabled = !_busy && !HasPendingEdits && _project?.CanRedo == true;
        _menu.IsEnabled = !_busy;
        _redo.IsEnabled = !_busy && !HasPendingEdits && _project?.CanRedo == true;
        _assetTabs.IsEnabled = !_busy;
        _modelEditor.IsEnabled = !_busy;
        _atlasEditor.IsEnabled = !_busy;
        _animationEditor.IsEnabled = !_busy;
        ClientContent.IsEnabled = !_busy;
        ClientContent.Inspector.IsEnabled = !_busy;
        _assets.IsEnabled = !_busy;
        _cancel.IsEnabled = _busy;
        _cancel.IsVisible = _busy;
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
            await ExecuteAsync(async token => close = await CanReplaceProject(token), commitEdits: false);
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
