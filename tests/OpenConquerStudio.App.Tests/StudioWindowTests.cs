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

public sealed partial class StudioWindowTests
{
    private static readonly Lazy<HeadlessUnitTestSession> s_session = new(() => HeadlessUnitTestSession.StartNew(typeof(StudioWindowTests)));

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<StudioApp>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).UseSkia();

    [Fact]
    public Task SaveCommitsVisibleFieldsWithoutAnApplyClick() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        directory.CreateSavedTexture();
        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Project, token));
            Find<ListBox>(window, "AssetBrowser").SelectedIndex = 0;
            await window.PendingOperation;
            Find<TextBox>(window, "AssetName").Text = "Saved directly";
            Find<TextBox>(window, "ProjectName").Text = "Renamed game";
            Assert.StartsWith("* ", window.Title);
            Find<CheckBox>(window, "FlipHorizontal").IsChecked = true;
            window.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Save")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.DoesNotContain("* ", window.Title);
        }
        finally { window.Close(); }
        using StudioProject project = StudioProject.Open(directory.Project);
        Assert.Equal("Renamed game", project.Document.Name);
        Assert.Equal("Saved directly", Assert.Single(project.Document.Assets).Name);
        Assert.True(project.Document.Assets[0].FlipHorizontal);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ProjectRenameUndoRedoRestoresVisibleNameAndPersistsUndoneRevision() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        directory.CreateSavedTexture();
        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Manifest, token));
            TextBox name = Find<TextBox>(window, "ProjectName");
            Button undo = Find<Button>(window, "Undo");
            Button redo = Find<Button>(window, "Redo");
            Button save = window.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Save"));

            name.Text = "Draft";
            undo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Equal("Game", name.Text);
            Assert.False(window.Project.IsModified);

            name.Text = "Renamed game";
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Equal("Renamed game", window.Project.Document.Name);
            Assert.False(window.Project.IsModified);

            undo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Equal("Game", window.Project.Document.Name);
            Assert.Equal("Game", name.Text);
            Assert.True(window.Project.IsModified);
            Assert.True(redo.IsEnabled);

            redo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Equal("Renamed game", window.Project.Document.Name);
            Assert.Equal("Renamed game", name.Text);
            Assert.False(window.Project.IsModified);

            undo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Equal("Game", window.Project.Document.Name);
            Assert.False(window.Project.IsModified);
        }
        finally { window.Close(); }

        using StudioProject reopened = StudioProject.Open(directory.Project);
        Assert.Equal("Game", reopened.Document.Name);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SourceTabCannotLeaveAnAuthoredInspectorActive() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        directory.CreateSavedTexture();
        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Project, token));
            Find<ListBox>(window, "AssetBrowser").SelectedIndex = 0;
            await window.PendingOperation;
            Assert.True(Find<TextBox>(window, "AssetName").IsEffectivelyVisible);
            Find<TabControl>(window, "ProjectContentTabs").SelectedIndex = 1;
            await window.PendingOperation;
            Assert.False(Find<TextBox>(window, "AssetName").IsEffectivelyVisible);
            Find<TabControl>(window, "ProjectContentTabs").SelectedIndex = 0;
            await window.PendingOperation;
            Assert.True(Find<TextBox>(window, "AssetName").IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task InvalidDraftSurvivesSaveAndSelectionFailure() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            project.CreateTexture("First", 1, 1, 1, 2, 3);
            project.CreateTexture("Second", 1, 1, 4, 5, 6);
            project.Save();
        }
        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Project, token));
            ListBox browser = Find<ListBox>(window, "AssetBrowser");
            browser.SelectedIndex = 0;
            await window.PendingOperation;
            Guid original = Assert.IsType<ResourceInfo>(browser.SelectedItem).Id;
            Find<TextBox>(window, "AssetName").Text = "";
            byte[] index = File.ReadAllBytes(directory.Manifest);
            window.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Save")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Equal(index, File.ReadAllBytes(directory.Manifest));
            Assert.Equal("", Find<TextBox>(window, "AssetName").Text);
            browser.SelectedIndex = 1;
            await window.PendingOperation;
            Assert.Equal(original, Assert.IsType<ResourceInfo>(browser.SelectedItem).Id);
            Assert.Equal("", Find<TextBox>(window, "AssetName").Text);
            Find<TextBox>(window, "AssetName").Text = window.Project.Document.Assets.Single(a => a.Id == original).Name;
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task OpeningTheActiveProjectKeepsItsSession() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        directory.CreateSavedTexture();
        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Project, token));
            StudioProject original = window.Project;
            await window.ExecuteAsync(token => window.LoadProject(directory.Manifest, token));
            Assert.Same(original, window.Project);
            Assert.DoesNotContain("in use", Find<TextBox>(window, "Diagnostics").Text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Already open", Find<TextBox>(window, "Diagnostics").Text);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ModelInspectorEditsPlaybackAndPersistsSourceData() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        OpenConquer.Content.Mesh.C3ModelData original = C3ModelFixture.Create();
        Guid id;
        using (StudioProject project = StudioProject.Create(directory.Project, "Characters"))
        {
            id = project.ImportModel("Actor", original);
            project.Save();
        }
        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Project, token));
            Find<ListBox>(window, "AssetBrowser").SelectedIndex = 0;
            await window.PendingOperation;
            Find<TextBox>(window, "ModelName").Text = "Edited actor";
            Find<NumericUpDown>(window, "ModelFrameMilliseconds").Value = 42;
            Find<CheckBox>(window, "ModelRepeat").IsChecked = false;
            Find<Button>(window, "ApplyModelEdits").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Find<Slider>(window, "AnimationPosition").Value = 84;
            Assert.Contains("Frame 2", Find<TextBlock>(window, "ModelBoneMatrix").Text);
            Assert.Contains("4.0000", Find<TextBlock>(window, "ModelBoneMatrix").Text);
            Find<CheckBox>(window, "ShowModelBones").IsChecked = true;
            Button save = window.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Save"));
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
        }
        finally { window.Close(); }
        using StudioProject reopened = StudioProject.Open(directory.Project);
        Assert.Equal("Edited actor", Assert.Single(reopened.Document.Models).Name);
        Assert.Equal(42, reopened.PreviewModel(id).Definition.Clips[0].FrameMilliseconds);
        Assert.Equal(original.Source("c3/body.c3").ToArray(), reopened.PreviewModel(id).Source("c3/body.c3").ToArray());
    }, TestContext.Current.CancellationToken);

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
        MenuItem catalog = window.GetLogicalDescendants().OfType<MenuItem>().Single(b => Equals(b.Header, "Client artwork"));
        catalog.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
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
            if (content is "New atlas" or "New animation")
            {
                window.GetLogicalDescendants().OfType<MenuItem>().Single(b => Equals(b.Header, content)).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            }
            else
            {
                window.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, content)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
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
