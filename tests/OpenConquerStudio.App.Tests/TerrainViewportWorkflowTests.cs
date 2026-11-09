using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.Tests;

namespace OpenConquerStudio.App.Tests;

public sealed partial class StudioWindowTests
{
    [Fact]
    public Task TerrainViewportPaintsARegionAsOneUndoableAndPersistentEdit() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        Guid terrainId;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            terrainId = project.CreateTerrain("Ground", 9100, 5, 4, new TerrainCell(1, 0, 0));
            project.Save(TestContext.Current.CancellationToken);
        }

        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Manifest, token));
            Assert.True(Find<Button>(window, "PaintTerrainSelection").IsEffectivelyVisible);
            Find<NumericUpDown>(window, "TerrainSelectionStartX").Value = 1;
            Find<NumericUpDown>(window, "TerrainSelectionStartY").Value = 1;
            Find<NumericUpDown>(window, "TerrainSelectionEndX").Value = 3;
            Find<NumericUpDown>(window, "TerrainSelectionEndY").Value = 2;
            Find<TextBox>(window, "TerrainName").Text = "Painted ground";
            Find<NumericUpDown>(window, "TerrainSurface").Value = 42;
            Find<NumericUpDown>(window, "TerrainPassability").Value = 65535;
            Find<NumericUpDown>(window, "TerrainElevation").Value = -17;

            Find<Button>(window, "PaintTerrainSelection").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Equal("Painted ground", window.Project.Document.Terrains.Single().Name);
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    TerrainCell expected = x is >= 1 and <= 3 && y is >= 1 and <= 2
                        ? new TerrainCell(42, 65535, -17) : new TerrainCell(1, 0, 0);
                    Assert.Equal(expected, window.Project.ReadTerrainCell(terrainId, x, y));
                }
            }

            Find<Button>(window, "Undo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Equal("Ground", window.Project.Document.Terrains.Single().Name);
            Assert.Equal(new TerrainCell(1, 0, 0), window.Project.ReadTerrainCell(terrainId, 2, 2));
            Assert.True(Find<Button>(window, "Redo").IsEnabled);
            Find<Button>(window, "Redo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Equal(new TerrainCell(42, 65535, -17), window.Project.ReadTerrainCell(terrainId, 2, 2));

            window.GetLogicalDescendants().OfType<Button>().Single(button => Equals(button.Content, "Save"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.False(window.Project.IsModified);
        }
        finally { window.Close(); }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        Assert.Equal("Painted ground", reopened.Document.Terrains.Single().Name);
        Assert.Equal(new TerrainCell(42, 65535, -17), reopened.ReadTerrainCell(terrainId, 2, 2));
        Assert.Equal(new TerrainCell(1, 0, 0), reopened.ReadTerrainCell(terrainId, 0, 0));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task TerrainViewportRejectsInvalidSelectionWithoutAlteringData() => s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            project.CreateTerrain("Ground", 9200, 2, 2, new TerrainCell(3, 1, 0));
            project.Save(TestContext.Current.CancellationToken);
        }

        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Manifest, token));
            Find<NumericUpDown>(window, "TerrainSelectionEndX").Text = "invalid";
            Find<Button>(window, "PaintTerrainSelection").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.False(window.Project.IsModified);
            Assert.Contains("selection end X", Find<TextBox>(window, "Diagnostics").Text, StringComparison.OrdinalIgnoreCase);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
