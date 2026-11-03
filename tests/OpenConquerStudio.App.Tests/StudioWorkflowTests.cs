using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.Runtime;
using OpenConquerStudio.Tests;

namespace OpenConquerStudio.App.Tests;

public sealed partial class StudioWindowTests
{
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

}
