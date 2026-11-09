using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.Tests;

namespace OpenConquerStudio.App.Tests;

public sealed partial class StudioWindowTests
{
    [Fact]
    public Task RefreshingToSmallerSourceGridKeepsInspectorSelectionInBounds() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        Guid id;
        TerrainImport initial = new(9400, 4, 4,
            [.. Enumerable.Repeat(new TerrainCell(3, 2, -1), 16)], [],
            new TerrainSource("maps/ground.dmap", "puzzles/ground.pul", 0, 1000, 0, 0, 0));
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            id = project.ImportTerrain(initial, "Ground");
            project.Save(TestContext.Current.CancellationToken);
        }

        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Manifest, token));
            Find<NumericUpDown>(window, "TerrainCellX").Value = 3;
            Find<NumericUpDown>(window, "TerrainCellY").Value = 3;
            Find<Button>(window, "InspectTerrainCell").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;

            window.Project.RefreshTerrain(id, initial with
            {
                Width = 2,
                Height = 2,
                Cells = [.. Enumerable.Repeat(new TerrainCell(7, 4, -5), 4)]
            });
            await window.SelectAsset(id, TestContext.Current.CancellationToken);

            Assert.Equal(1, Find<NumericUpDown>(window, "TerrainCellX").Value);
            Assert.Equal(1, Find<NumericUpDown>(window, "TerrainCellY").Value);
            Assert.True(Find<Button>(window, "PaintTerrainSelection").IsEffectivelyVisible);
            Assert.Equal(new TerrainCell(7, 4, -5), window.Project.ReadTerrainCell(id, 1, 1));

            Find<Button>(window, "Undo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Equal((4, 4), (window.Project.Document.Terrains.Single().Width,
                window.Project.Document.Terrains.Single().Height));

            Find<Button>(window, "Redo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Equal((2, 2), (window.Project.Document.Terrains.Single().Width,
                window.Project.Document.Terrains.Single().Height));
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
