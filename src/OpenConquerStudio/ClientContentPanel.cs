using System.Collections.Immutable;
using System.Globalization;
using Avalonia.Controls;
using OpenConquerStudio.ConquerImport;

namespace OpenConquerStudio;

internal sealed record ClientAssetOptions(string? Section, int FrameMilliseconds, uint HairStyle, uint BodyModelId, uint ArmetModelId);

internal sealed class ClientContentPanel : Grid
{
    private readonly TextBox _filter = new() { PlaceholderText = "Filter client paths…", Name = "ClientContentFilter" };
    private readonly ComboBox _kind = new() { ItemsSource = new[] { "All", "map", "animation", "texture", "puzzle", "character", "model", "scene", "archive", "audio", "configuration", "source" }, SelectedIndex = 0 };
    private readonly ListBox _sources = new() { Name = "ClientContentBrowser" };
    private readonly ComboBox _sections = new() { Name = "ClientAnimationSection" };
    private readonly NumericUpDown _duration = new() { Value = 100, Minimum = 1, Maximum = 60000, FormatString = "0", ParsingNumberStyle = NumberStyles.Integer };
    private readonly NumericUpDown _x = new() { Minimum = 0, Maximum = 65535, Value = 0, FormatString = "0", ParsingNumberStyle = NumberStyles.Integer };
    private readonly NumericUpDown _y = new() { Minimum = 0, Maximum = 65535, Value = 0, FormatString = "0", ParsingNumberStyle = NumberStyles.Integer };
    private readonly TextBlock _details = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly StackPanel _clipControls;
    private readonly StackPanel _mapControls;
    private readonly NumericUpDown _hair = new() { Minimum = 0, Maximum = 999, Value = 0, Name = "CharacterHairStyle" };
    private readonly NumericUpDown _bodyModel = new() { Minimum = 0, Maximum = uint.MaxValue, Value = 0, Name = "BodyCatalogId" };
    private readonly NumericUpDown _armetModel = new() { Minimum = 0, Maximum = uint.MaxValue, Value = 0, Name = "ArmetCatalogId" };
    private readonly StackPanel _characterControls;
    private readonly Button _import = new() { Content = "Import editable copy", Name = "ImportClientAsset" };
    private readonly Button _replace = new() { Content = "Replace selected authored texture", Name = "ReimportClientImage" };
    private ImmutableArray<ClientAssetEntry> _inventory = [];
    private bool _loading;

    public ClientContentPanel(Func<ClientAssetEntry, ClientAssetOptions, Task> preview,
        Func<ClientAssetEntry, ClientAssetOptions, bool, Task> import, Action<int, int> moveCamera)
    {
        RowDefinitions = new RowDefinitions("Auto,Auto,*");
        RowSpacing = 6;
        Children.Add(_filter);
        Children.Add(_kind);
        SetRow(_kind, 1);
        Children.Add(_sources);
        SetRow(_sources, 2);
        Button camera = new()
        {
            Content = "Go to map tile",
            Name = "GoToMapTile"
        };
        camera.Click += (_, _) => moveCamera((int)(_x.Value ?? 0), (int)(_y.Value ?? 0));
        _mapControls = new StackPanel { Spacing = 4, Children = { new TextBlock { Text = "Camera tile X / Y" }, _x, _y, camera }, IsVisible = false };
        _clipControls = new StackPanel { Spacing = 4, Children = { new TextBlock { Text = "ANI section" }, _sections, new TextBlock { Text = "Authored frame duration (milliseconds)" }, _duration }, IsVisible = false };
        _characterControls = new StackPanel
        {
            Spacing = 4,
            IsVisible = false,
            Children = { new TextBlock { Text = "Hair style (0 = none; catalog ID suffix)" }, _hair,
                new TextBlock { Text = "Body catalog ID (0 = base body)" }, _bodyModel,
                new TextBlock { Text = "Headwear catalog ID (0 = hair selection)" }, _armetModel }
        };
        Button updateCharacter = new()
        {
            Content = "Preview character",
            Name = "PreviewCharacter"
        };
        updateCharacter.Click += async (_, _) =>
        {
            if (_sources.SelectedItem is ClientAssetEntry entry)
            {
                await preview(entry, Options());
            }
        };
        _characterControls.Children.Add(updateCharacter);
        Inspector = new StackPanel
        {
            Spacing = 8,
            Children = { _details, _clipControls, _mapControls, _characterControls, _import, _replace },
            IsVisible = false,
        };
        _filter.TextChanged += (_, _) => Filter();
        _kind.SelectionChanged += (_, _) => Filter();
        _sources.SelectionChanged += async (_, _) =>
        {
            if (!_loading && _sources.SelectedItem is ClientAssetEntry entry)
            {
                Inspector.IsVisible = true;
                _clipControls.IsVisible = entry.Kind == "animation";
                _mapControls.IsVisible = entry.Kind == "map";
                _characterControls.IsVisible = entry.Kind == "character";
                _import.IsVisible = entry.Kind is "texture" or "animation" or "model" or "character";
                _replace.IsVisible = entry.Kind == "texture";
                await preview(entry, Options() with
                {
                    Section = null
                });
            }
        };
        _sections.SelectionChanged += async (_, _) =>
        {
            if (!_loading && _sources.SelectedItem is ClientAssetEntry entry && _sections.SelectedItem is string section)
            {
                await preview(entry, Options() with
                {
                    Section = section
                });
            }
        };
        _import.Click += async (_, _) =>
        {
            if (_sources.SelectedItem is ClientAssetEntry entry)
            {
                await import(entry, Options(), false);
            }
        };
        _replace.Click += async (_, _) =>
        {
            if (_sources.SelectedItem is ClientAssetEntry entry)
            {
                await import(entry, Options() with
                {
                    Section = null
                }, true);
            }
        };
        IsVisible = false;
    }

    private ClientAssetOptions Options() => new(_sections.SelectedItem as string, (int)(_duration.Value ?? 100),
        (uint)(_hair.Value ?? 0), (uint)(_bodyModel.Value ?? 0), (uint)(_armetModel.Value ?? 0));

    public Control Inspector
    {
        get;
    }

    public void Load(ImmutableArray<ClientAssetEntry> inventory, string root)
    {
        _inventory = inventory;
        IsVisible = true;
        _details.Text = root + "\n" + inventory.Length + " source entries. Source files remain unchanged.";
        Filter();
        _import.IsVisible = false;
        _replace.IsVisible = false;
    }

    public string? SetSections(ImmutableArray<string> sections)
    {
        _loading = true;
        try
        {
            _sections.ItemsSource = sections;
            _sections.SelectedIndex = sections.IsEmpty ? -1 : 0;
        }
        finally
        {
            _loading = false;
        }

        return _sections.SelectedItem as string;
    }

    public void Describe(string text) => _details.Text = text;

    public void SetCamera(int x, int y, int width, int height)
    {
        _x.Maximum = Math.Min(65535, width - 1);
        _y.Maximum = Math.Min(65535, height - 1);
        _x.Value = x;
        _y.Value = y;
    }

    private void Filter()
    {
        string filter = _filter.Text ?? "";
        string? kind = _kind.SelectedItem as string;
        _loading = true;
        try
        {
            _sources.ItemsSource = _inventory.Where(a => (kind is null or "All" || a.Kind == kind) && a.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();
        }
        finally
        {
            _loading = false;
        }
    }
}
