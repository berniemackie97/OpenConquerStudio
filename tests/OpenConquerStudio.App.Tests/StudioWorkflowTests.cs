using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.ConquerImport;
using OpenConquerStudio.Runtime;
using OpenConquerStudio.Tests;

namespace OpenConquerStudio.App.Tests;

public sealed partial class StudioWindowTests
{
    [Fact]
    public Task FilteringClientSourcesClearsPreviewAndRejectsInvalidCameraInput() => s_session.Value.Dispatch(async () =>
    {
        int cleared = 0;
        int cameraMoves = 0;
        ClientContentPanel panel = new((_, _) => Task.CompletedTask, (_, _, _) => Task.CompletedTask,
            (_, _) => cameraMoves++, () => cleared++);
        Window window = new()
        {
            Content = new StackPanel
            {
                Children = { panel, panel.Inspector }
            }
        };
        window.Show();
        try
        {
            panel.Load([new ClientAssetEntry("map", "game/map.dmap", 1010)], "sources");
            Find<ListBox>(window, "ClientContentBrowser").SelectedIndex = 0;
            Find<NumericUpDown>(window, "MapCameraTileX").Text = "invalid";
            Find<Button>(window, "GoToMapTile").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(0, cameraMoves);

            Find<TextBox>(window, "ClientContentFilter").Text = "not present";
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { });
            Assert.Empty(Find<ListBox>(window, "ClientContentBrowser").Items);
            Assert.Null(Find<ListBox>(window, "ClientContentBrowser").SelectedItem);
            Assert.Equal(1, cleared);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task InvalidDraftCanBeUndoneWithoutChangingSavedContent() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        directory.CreateSavedTexture();
        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Manifest, token));
            Find<TextBox>(window, "AssetName").Text = "";
            Assert.True(Find<Button>(window, "Undo").IsEnabled);
            Find<Button>(window, "Undo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Equal("Color", Find<TextBox>(window, "AssetName").Text);
            Assert.False(window.Project.IsModified);
            Assert.False(Find<Button>(window, "Undo").IsEnabled);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ClosingInvalidDraftAllowsCancelAndDiscard() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        directory.CreateSavedTexture();
        byte[] saved = File.ReadAllBytes(directory.Manifest);
        StudioWindow window = new();
        window.Show();
        await window.ExecuteAsync(token => window.LoadProject(directory.Manifest, token));
        Find<TextBox>(window, "AssetName").Text = "";
        window.Close();
        Window cancel = Assert.Single(window.OwnedWindows);
        cancel.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Cancel")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await window.PendingOperation;
        Assert.True(window.IsVisible);
        Assert.Equal("", Find<TextBox>(window, "AssetName").Text);
        window.Close();
        Window discard = Assert.Single(window.OwnedWindows);
        discard.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Discard")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await window.PendingOperation;
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { });
        Assert.False(window.IsVisible);
        Assert.Equal(saved, File.ReadAllBytes(directory.Manifest));
        using StudioProject reopened = StudioProject.Open(directory.Project);
        Assert.Equal("Color", Assert.Single(reopened.Document.Assets).Name);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task BuildSavesVisibleEditsAndCreatesFreshValidatedOutputs() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        directory.CreateSavedTexture();
        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Manifest, token));
            Find<TextBox>(window, "AssetName").Text = "Built directly";
            Assert.False(Directory.Exists(Path.Combine(directory.Project, "Builds")));
            await window.ExecuteAsync(window.Build);
            await window.ExecuteAsync(window.Build);
            string[] builds = Directory.GetDirectories(Path.Combine(directory.Project, "Builds"));
            Assert.Equal(2, builds.Length);
            Assert.All(builds, build => Assert.Equal("Built directly", Assert.Single(ContentPackage.Load(build, TestContext.Current.CancellationToken).Manifest.Textures).Name));
            Assert.Equal(File.ReadAllBytes(Path.Combine(builds[0], "content.json")), File.ReadAllBytes(Path.Combine(builds[1], "content.json")));
            Assert.False(window.Project.IsModified);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SpriteNavigationCommitsFieldsAndRejectsInvalidNumericText() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        using (StudioProject project = StudioProject.Create(directory.Project, "Sprites"))
        {
            Guid texture = project.CreateTexture("Texture", 8, 8, 1, 2, 3);
            Guid atlas = project.CreateAtlas("Atlas", texture);
            AtlasAsset original = Assert.Single(project.Document.Atlases);
            project.EditAtlas(original with
            {
                Sprites = [original.Sprites[0], original.Sprites[0] with { Id = Guid.NewGuid(), Name = "Second" }]
            });
            project.Save();
        }
        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Manifest, token));
            ListBox assets = Find<ListBox>(window, "AssetBrowser");
            assets.SelectedItem = assets.Items.OfType<ResourceInfo>().Single(r => r.Kind == "atlas");
            await window.PendingOperation;
            ListBox sprites = Find<ListBox>(window, "SpriteList");
            Find<TextBox>(window, "SpriteName").Text = "First edited";
            sprites.SelectedIndex = 1;
            await window.PendingOperation;
            Assert.Equal("First edited", window.Project.Document.Atlases[0].Sprites[0].Name);
            Assert.Equal("Second", Find<TextBox>(window, "SpriteName").Text);
            Find<NumericUpDown>(window, "SpriteWidth").Text = "broken";
            sprites.SelectedIndex = 0;
            await window.PendingOperation;
            Assert.Equal(1, sprites.SelectedIndex);
            Assert.Equal("broken", Find<NumericUpDown>(window, "SpriteWidth").Text);
            Find<Button>(window, "Undo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Find<Button>(window, "BuildProject").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Equal(8, window.Project.Document.Atlases[0].Sprites[1].Width);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task MissingClientSourcesDoNotBlockAuthoredWorkspace() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        directory.CreateSavedTexture();
        string user = Path.Combine(directory.Project, ".user");
        Directory.CreateDirectory(user);
        File.WriteAllText(Path.Combine(user, "client.json"), "{\"schemaVersion\":1,\"repositoryPath\":\"missing-repository\",\"contentPath\":\"missing-content\"}");
        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Manifest, token));
            Assert.Equal("Color", Find<TextBox>(window, "AssetName").Text);
            Assert.Contains("Client sources unavailable", Find<TextBox>(window, "Diagnostics").Text);
            Assert.Equal(0, Find<TabControl>(window, "ProjectContentTabs").SelectedIndex);
            Find<TabControl>(window, "ProjectContentTabs").SelectedIndex = 1;
            await window.PendingOperation;
            MenuItem delete = window.GetLogicalDescendants().OfType<MenuItem>().Single(m => Equals(m.Header, "Delete resource"));
            Assert.False(delete.IsEnabled);
            Assert.False(Find<TextBox>(window, "AssetName").IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
    [Fact]
    public Task FrameNavigationPreservesSelectionAndCommitsPreviousDuration() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        using (StudioProject project = StudioProject.Create(directory.Project, "Frames"))
        {
            Guid texture = project.CreateTexture("Texture", 8, 8, 1, 2, 3);
            Guid atlas = project.CreateAtlas("Atlas", texture);
            AtlasAsset original = Assert.Single(project.Document.Atlases);
            project.EditAtlas(original with
            {
                Sprites = [original.Sprites[0], original.Sprites[0] with { Id = Guid.NewGuid(), Name = "Second" }]
            });
            project.CreateAnimation("Clip", atlas);
            project.Save();
        }
        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Manifest, token));
            ListBox assets = Find<ListBox>(window, "AssetBrowser");
            assets.SelectedItem = assets.Items.OfType<ResourceInfo>().Single(r => r.Kind == "animation");
            await window.PendingOperation;
            ListBox frames = Find<ListBox>(window, "AnimationFrames");
            Find<NumericUpDown>(window, "FrameDuration").Value = 250;
            frames.SelectedIndex = 1;
            await window.PendingOperation;
            Assert.Equal(1, frames.SelectedIndex);
            Assert.Equal(250, window.Project.Document.Animations[0].Frames[0].DurationTicks);
            Find<NumericUpDown>(window, "FrameDuration").Value = 350;
            window.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Save")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Equal(350, window.Project.Document.Animations[0].Frames[1].DurationTicks);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CancelingNewProjectBeforeChoosingLocationLeavesDraftUnchanged() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        directory.CreateSavedTexture();
        byte[] saved = File.ReadAllBytes(directory.Manifest);
        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Manifest, token));
            Find<TextBox>(window, "AssetName").Text = "Still editing";
            MenuItem create = window.GetLogicalDescendants().OfType<MenuItem>().Single(m => Equals(m.Header, "New project"));
            create.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { });

            Window dialog = Assert.Single(window.OwnedWindows);
            Assert.Equal("New project", dialog.Title);
            dialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Cancel"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;

            Assert.Equal("Still editing", Find<TextBox>(window, "AssetName").Text);
            Assert.False(window.Project.IsModified);
            Assert.Equal(saved, File.ReadAllBytes(directory.Manifest));
        }
        finally
        {
            Find<TextBox>(window, "AssetName").Text = "Color";
            window.Close();
        }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ReconnectingSourceInventoryClearsPreviousSelection() => s_session.Value.Dispatch(async () =>
    {
        int cleared = 0;
        ClientContentPanel panel = new((_, _) => Task.CompletedTask, (_, _, _) => Task.CompletedTask,
            (_, _) => { }, () => cleared++);
        Window window = new()
        {
            Content = new StackPanel { Children = { panel, panel.Inspector } }
        };
        window.Show();
        try
        {
            ClientAssetEntry entry = new("map", "game/map.dmap", 1010);
            panel.Load([entry], "first source");
            ListBox browser = Find<ListBox>(window, "ClientContentBrowser");
            browser.SelectedIndex = 0;
            Assert.True(panel.Inspector.IsVisible);
            int previous = cleared;

            panel.Load([entry], "second source");
            Assert.Null(browser.SelectedItem);
            Assert.False(panel.Inspector.IsVisible);
            Assert.Equal(previous + 1, cleared);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ClearingAuthoredSelectionCommitsDraftAndUndoRestoresIt() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        directory.CreateSavedTexture();
        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Manifest, token));
            ListBox browser = Find<ListBox>(window, "AssetBrowser");
            browser.SelectedIndex = 0;
            await window.PendingOperation;
            Find<TextBox>(window, "AssetName").Text = "Edited without Apply";

            browser.SelectedIndex = -1;
            await window.PendingOperation;

            Assert.Null(browser.SelectedItem);
            Assert.False(Find<TextBox>(window, "AssetName").IsEffectivelyVisible);
            Assert.True(Find<TextBlock>(window, "EmptyPreview").IsVisible);
            Assert.Equal("Edited without Apply", Assert.Single(window.Project.Document.Assets).Name);
            Assert.True(Find<Button>(window, "Undo").IsEnabled);

            Find<Button>(window, "Undo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;

            Assert.Null(browser.SelectedItem);
            Assert.Equal("Color", Assert.Single(window.Project.Document.Assets).Name);
            Assert.False(window.Project.IsModified);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ClearingAuthoredSelectionRejectsInvalidDraftWithoutLosingIt() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        directory.CreateSavedTexture();
        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Manifest, token));
            ListBox browser = Find<ListBox>(window, "AssetBrowser");
            browser.SelectedIndex = 0;
            await window.PendingOperation;
            Guid original = Assert.IsType<ResourceInfo>(browser.SelectedItem).Id;
            Find<TextBox>(window, "AssetName").Text = "";

            browser.SelectedIndex = -1;
            await window.PendingOperation;

            Assert.Equal(original, Assert.IsType<ResourceInfo>(browser.SelectedItem).Id);
            Assert.Equal("", Find<TextBox>(window, "AssetName").Text);
            Assert.True(Find<TextBox>(window, "AssetName").IsEffectivelyVisible);
            Assert.Equal("Color", Assert.Single(window.Project.Document.Assets).Name);
            Assert.False(window.Project.IsModified);
        }
        finally
        {
            Find<TextBox>(window, "AssetName").Text = "Color";
            window.Close();
        }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ClearingClientSelectionHidesItsInspectorAndActions() => s_session.Value.Dispatch(async () =>
    {
        int cleared = 0;
        ClientContentPanel panel = new((_, _) => Task.CompletedTask, (_, _, _) => Task.CompletedTask,
            (_, _) => { }, () => cleared++);
        Window window = new()
        {
            Content = new StackPanel { Children = { panel, panel.Inspector } }
        };
        window.Show();
        try
        {
            panel.Load([new ClientAssetEntry("texture", "artwork/image.tga")], "sources");
            ListBox browser = Find<ListBox>(window, "ClientContentBrowser");
            browser.SelectedIndex = 0;
            Assert.True(panel.Inspector.IsVisible);
            Assert.True(Find<Button>(window, "ImportClientAsset").IsVisible);

            browser.SelectedIndex = -1;

            Assert.Null(browser.SelectedItem);
            Assert.False(panel.Inspector.IsVisible);
            Assert.False(Find<Button>(window, "ImportClientAsset").IsVisible);
            Assert.False(Find<Button>(window, "ReimportClientAsset").IsVisible);
            Assert.Equal(1, cleared);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);



    [Fact]
    public Task RefreshAnimationActionPreservesChosenAniSection() => s_session.Value.Dispatch(async () =>
    {
        string? section = null;
        bool replacement = false;
        ClientContentPanel panel = new((_, _) => Task.CompletedTask, (_, read, replace) =>
        {
            section = read().Section;
            replacement = replace;
            return Task.CompletedTask;
        }, (_, _) => { }, () => { });
        Window window = new()
        {
            Content = new StackPanel { Children = { panel, panel.Inspector } }
        };
        window.Show();
        try
        {
            panel.Load([new ClientAssetEntry("animation", "ani/idle.ani")], "sources");
            Find<ListBox>(window, "ClientContentBrowser").SelectedIndex = 0;
            panel.SetSections(["Idle"]);
            Button refresh = Find<Button>(window, "ReimportClientAsset");
            Assert.True(refresh.IsVisible);
            Assert.Equal("Refresh selected authored animation", refresh.Content);
            refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { });
            Assert.True(replacement);
            Assert.Equal("Idle", section);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task TerrainInspectorCommitsCellNavigationAndSaveWithoutLosingCoordinates() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        Guid id;
        using (StudioProject project = StudioProject.Create(directory.Project, "World"))
        {
            id = project.CreateTerrain("Ground", 8100, 3, 2, new TerrainCell(1, 0, -10));
            project.Save(TestContext.Current.CancellationToken);
        }
        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Manifest, token));
            Find<ListBox>(window, "AssetBrowser").SelectedItem =
                Find<ListBox>(window, "AssetBrowser").Items.OfType<ResourceInfo>().Single(r => r.Id == id);
            await window.PendingOperation;
            Assert.True(Find<NumericUpDown>(window, "TerrainSurface").IsEffectivelyVisible);
            Find<NumericUpDown>(window, "TerrainSurface").Value = 44;
            Find<NumericUpDown>(window, "TerrainCellX").Value = 2;
            Find<NumericUpDown>(window, "TerrainCellY").Value = 1;
            Find<Button>(window, "InspectTerrainCell").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Equal(new TerrainCell(44, 0, -10), window.Project.ReadTerrainCell(id, 0, 0));
            Find<NumericUpDown>(window, "TerrainSurface").Value = 99;
            Find<NumericUpDown>(window, "TerrainElevation").Value = short.MinValue;
            Find<Button>(window, "ApplyTerrainEdits").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Equal(new TerrainCell(99, 0, short.MinValue), window.Project.ReadTerrainCell(id, 2, 1));
            Find<TextBox>(window, "TerrainName").Text = "Ground edited";
            window.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Save"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.False(window.Project.IsModified);
            Assert.Equal("Ground edited", Assert.Single(window.Project.Document.Terrains).Name);
        }
        finally { window.Close(); }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        Assert.Equal(id, Assert.Single(reopened.Document.Terrains).Id);
        Assert.Equal(new TerrainCell(44, 0, -10), reopened.ReadTerrainCell(id, 0, 0));
        Assert.Equal(new TerrainCell(99, 0, short.MinValue), reopened.ReadTerrainCell(id, 2, 1));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task InvalidTerrainDraftPreventsNavigationAndRetainsInspector() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        Guid id;
        using (StudioProject project = StudioProject.Create(directory.Project, "World"))
        {
            id = project.CreateTerrain("Ground", 8200, 2, 2, new TerrainCell(1, 0, 0));
            project.Save(TestContext.Current.CancellationToken);
        }
        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Manifest, token));
            Find<ListBox>(window, "AssetBrowser").SelectedIndex = 0;
            await window.PendingOperation;
            Find<NumericUpDown>(window, "TerrainSurface").Text = "invalid";
            Find<NumericUpDown>(window, "TerrainCellX").Value = 1;
            Find<Button>(window, "InspectTerrainCell").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.True(Find<NumericUpDown>(window, "TerrainSurface").IsEffectivelyVisible);
            Assert.Equal("invalid", Find<NumericUpDown>(window, "TerrainSurface").Text);
            Assert.Equal(new TerrainCell(1, 0, 0), window.Project.ReadTerrainCell(id, 0, 0));
            Assert.False(window.Project.IsModified);
            Find<Button>(window, "Undo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.False(window.Project.IsModified);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

}
