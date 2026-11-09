using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.Tests;

namespace OpenConquerStudio.App.Tests;

public sealed partial class StudioWindowTests
{
    [Fact]
    public Task TerrainInspectorPromotesSelectedFrameAsAnUndoableAuthoredTexture() =>
        s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        Guid terrain;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            terrain = project.ImportTerrain(OwnedMapVisualInputsTests.Input(), "Ground");
            project.Save(TestContext.Current.CancellationToken);
        }

        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Manifest, token));
            ListBox sourceFrames = Find<ListBox>(window, "TerrainSourceFrameSelection");
            Assert.Equal(2, sourceFrames.ItemCount);
            Assert.True(sourceFrames.IsEffectivelyVisible);
            sourceFrames.SelectedIndex = 0;
            Find<Button>(window, "PromoteTerrainFrames")
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            TextureAsset texture = Assert.Single(window.Project.Document.Assets);
            Assert.Equal(texture.Id, window.Project.Document.Terrains.Single()
                .Source!.Frames[0].TextureId);
            Assert.Equal(terrain, window.Project.Document.Terrains.Single().Id);
            Assert.True(window.Project.IsModified);

            Find<Button>(window, "Undo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Empty(window.Project.Document.Assets);
            Assert.Null(window.Project.Document.Terrains.Single().Source!.Frames[0].TextureId);
            Find<Button>(window, "Redo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Equal(texture.Id, Assert.Single(window.Project.Document.Assets).Id);

            await window.ExecuteAsync(token =>
            {
                window.Project.Save(token);
                return Task.CompletedTask;
            }, commitEdits: false);
            Assert.False(window.Project.IsModified);
        }
        finally
        {
            window.Close();
        }

        using StudioProject reopened = StudioProject.Open(directory.Project,
            TestContext.Current.CancellationToken);
        Assert.Single(reopened.Document.Assets);
        Assert.NotNull(reopened.Document.Terrains.Single().Source!.Frames[0].TextureId);
    }, TestContext.Current.CancellationToken);
}
