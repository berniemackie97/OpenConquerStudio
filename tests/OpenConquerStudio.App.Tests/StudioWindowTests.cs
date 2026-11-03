using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.ConquerImport;
using OpenConquerStudio.Tests;

namespace OpenConquerStudio.App.Tests;

public sealed class StudioWindowTests
{
    private static readonly Lazy<HeadlessUnitTestSession> s_session = new(() => HeadlessUnitTestSession.StartNew(typeof(StudioWindowTests)));

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<StudioApp>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).UseSkia();

    [Fact]
    public Task SelectEditSaveAndReopen_UpdatesPreviewAndPersists() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        Guid id;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            id = project.ImportTexture(directory.WriteTga(), "Two colors");
            project.Save();
        }

        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Project, token));
            ListBox browser = Find<ListBox>(window, "AssetBrowser");
            Assert.Single(browser.Items);
            browser.SelectedIndex = 0;
            await window.PendingOperation;
            Image preview = Find<Image>(window, "TexturePreview");
            Assert.Equal(new PixelSize(2, 1), Assert.IsType<WriteableBitmap>(preview.Source).PixelSize);
            Find<TextBox>(window, "AssetName").Text = "Edited";
            Find<CheckBox>(window, "FlipHorizontal").IsChecked = true;
            Find<ComboBox>(window, "Rotation").SelectedIndex = 1;
            Find<Button>(window, "ApplyAssetEdits").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Equal(new PixelSize(1, 2), Assert.IsType<WriteableBitmap>(preview.Source).PixelSize);
            WriteableBitmap bitmap = Assert.IsType<WriteableBitmap>(preview.Source);
            byte[] displayedPixels = new byte[8];
            using (Avalonia.Platform.ILockedFramebuffer framebuffer = bitmap.Lock())
            {
                Marshal.Copy(framebuffer.Address, displayedPixels, 0, 4);
                Marshal.Copy(framebuffer.Address + framebuffer.RowBytes, displayedPixels, 4, 4);
            }
            Assert.Equal(new byte[] { 0, 255, 0, 255, 255, 0, 0, 255 }, displayedPixels);
            Assert.StartsWith("* ", window.Title);
            Button save = window.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Save"));
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.DoesNotContain("Unsaved", window.Title);
        }
        finally
        {
            window.Close();
        }

        using StudioProject reopened = StudioProject.Open(directory.Project);
        TextureAsset asset = Assert.Single(reopened.Document.Assets);
        Assert.Equal(id, asset.Id);
        Assert.Equal("Edited", asset.Name);
        Assert.True(asset.FlipHorizontal);
        Assert.Equal(1, asset.ClockwiseQuarterTurns);
        Assert.Equal(new byte[] { 0, 255, 0, 255, 255, 0, 0, 255 }, reopened.Preview(id).Pixels.ToArray());
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task FailedProjectOpen_ShowsDiagnosticAndKeepsCurrentProject() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        directory.CreateSavedTexture();
        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Project, token));
            await window.ExecuteAsync(token => window.LoadProject(directory.PathFor("missing"), token));
            Assert.Single(Find<ListBox>(window, "AssetBrowser").Items);
            Assert.NotEmpty(Find<TextBox>(window, "Diagnostics").Text ?? "");
        }
        finally
        {
            window.Close();
        }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CloseDuringOperation_CancelsAndWaitsBeforeReleasingProject() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        directory.CreateSavedTexture();
        StudioWindow window = new();
        window.Show();
        await window.ExecuteAsync(token => window.LoadProject(directory.Project, token));
        TaskCompletionSource started = new();
        Task operation = window.ExecuteAsync(async token =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
        });
        await started.Task;
        window.Close();
        await operation;
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { });
        using StudioProject reopened = StudioProject.Open(directory.Project);
        Assert.Equal("Game", reopened.Document.Name);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ArtworkCatalog_ImportsExistingClientImageIntoProject() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
        }
        StudioWindow window = new();
        window.Show();
        await window.ExecuteAsync(token => window.LoadProject(directory.Project, token));
        Button catalog = window.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Client artwork"));
        catalog.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Window dialog = Assert.Single(window.OwnedWindows);
        ListBox list = Assert.Single(dialog.GetLogicalDescendants().OfType<ListBox>());
        Assert.Equal(8, list.Items.Count);
        list.SelectedIndex = 0;
        Button import = dialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Import selected image"));
        import.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await window.PendingOperation;
        Assert.Single(Find<ListBox>(window, "AssetBrowser").Items);
        Assert.Equal(new PixelSize(166, 25), Assert.IsType<WriteableBitmap>(Find<Image>(window, "TexturePreview").Source).PixelSize);
        Button save = window.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Save"));
        save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await window.PendingOperation;
        window.Close();
        using StudioProject reopened = StudioProject.Open(directory.Project);
        Assert.Equal("conquer/login/account/row", Assert.Single(reopened.Document.Assets).SourceKey);
    }, TestContext.Current.CancellationToken);

    private static T Find<T>(Control root, string name) where T : Control => root.GetLogicalDescendants().OfType<T>().Single(c => c.Name == name);

    [Fact]
    public Task ConnectedClientBrowser_ImportsActualAniAndReopensSavedAssets() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        using (StudioProject project = StudioProject.Create(directory.Project, "Client workspace"))
        {
            ClientProjectConnection.Attach(project, ProjectDirectory.RepositoryRoot,
                Path.Combine(ProjectDirectory.RepositoryRoot, "content", "retail-5517", "payload"), TestContext.Current.CancellationToken);
        }

        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Project, token));
            Find<TabControl>(window, "ProjectContentTabs").SelectedIndex = 1;
            Find<TextBox>(window, "ClientContentFilter").Text = "ani/control.ani";
            ListBox sources = Find<ListBox>(window, "ClientContentBrowser");
            Assert.Single(sources.Items);
            sources.SelectedIndex = 0;
            await window.PendingOperation;
            Find<ComboBox>(window, "ClientAnimationSection").SelectedItem = "Action_Act0Btn";
            await window.PendingOperation;
            Assert.NotNull(Find<StudioViewport>(window, "PreviewViewport").CurrentSprite);
            Find<Button>(window, "ImportClientAsset").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Equal(5, window.Project.Document.Resources.Count());
            Assert.Equal(2, Assert.Single(window.Project.Document.Animations).Frames.Length);
            Button save = window.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Save"));
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
        }
        finally
        {
            window.Close();
        }

        using StudioProject reopened = StudioProject.Open(directory.Project);
        Assert.Equal(2, reopened.Document.Assets.Length);
        Assert.Equal(2, Assert.Single(reopened.Document.Animations).Frames.Length);
        Assert.All(reopened.Document.Assets, asset => Assert.StartsWith("conquer/source/", asset.SourceKey));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task AuthorAtlasAndAnimation_EditScrubUndoSaveAndReopen() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        Guid textureId;
        using (StudioProject project = StudioProject.Create(directory.Project, "Animated game"))
        {
            textureId = project.ImportTexture(directory.WriteTga(), "Two colors");
            project.Save();
        }

        StudioWindow window = new();
        window.Show();
        async Task Click(string content)
        {
            window.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, content))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
        }

        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Project, token));
            ListBox browser = Find<ListBox>(window, "AssetBrowser");
            browser.SelectedIndex = 0;
            await window.PendingOperation;
            await Click("New atlas");
            Find<NumericUpDown>(window, "SpriteWidth").Value = 1;
            Find<NumericUpDown>(window, "PivotX").Value = 0;
            Find<TextBox>(window, "SpriteName").Text = "Red";
            await Click("Apply sprite edits");
            await Click("Add sprite");
            await window.PendingOperation;
            Find<TextBox>(window, "SpriteName").Text = "Green";
            Find<NumericUpDown>(window, "SpriteX").Value = 1;
            await Click("Apply sprite edits");
            await Click("New animation");
            Assert.Equal(3, browser.Items.Count);
            ListBox frames = Find<ListBox>(window, "AnimationFrames");
            Assert.Equal(2, frames.Items.Count);
            frames.SelectedIndex = 1;
            Find<NumericUpDown>(window, "FrameDuration").Value = 250;
            await Click("Update frame");
            Assert.Equal(1, Find<StudioViewport>(window, "PreviewViewport").FrameIndex);
            Find<Slider>(window, "AnimationPosition").Value = 100;
            StudioViewport viewport = Find<StudioViewport>(window, "PreviewViewport");
            Assert.Equal(1, viewport.FrameIndex);
            Assert.Equal("Green", viewport.CurrentSprite!.Region.Name);
            await Click("Up frame");
            Assert.Equal("Green", viewport.CurrentSprite!.Region.Name);
            await Click("Undo");
            Find<Slider>(window, "AnimationPosition").Value = 100;
            Assert.Equal("Green", viewport.CurrentSprite!.Region.Name);
            await Click("Save");
        }
        finally
        {
            window.Close();
        }

        using StudioProject reopened = StudioProject.Open(directory.Project);
        Assert.Equal(textureId, Assert.Single(reopened.Document.Atlases).TextureId);
        AnimationAsset animation = Assert.Single(reopened.Document.Animations);
        Assert.Collection(animation.Frames, frame => Assert.Equal(100, frame.DurationTicks), frame => Assert.Equal(250, frame.DurationTicks));
        Assert.Equal("Green", reopened.Document.Atlases[0].Sprites[1].Name);
    }, TestContext.Current.CancellationToken);
}
