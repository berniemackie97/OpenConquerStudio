using System.Buffers.Binary;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using OpenConquer.Content.Map;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.ConquerImport;
using OpenConquerStudio.Tests;

namespace OpenConquerStudio.App.Tests;

public sealed partial class StudioWindowTests
{
    [Fact]
    public Task SceneryPositionDraftAppliesAndSavesWithTheSelectedTerrain() =>
        s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        Guid terrainId;
        Guid placementId;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            terrainId = project.ImportTerrain(CapturedMapWithAnimatedScenery(), "Ground");
            placementId = Assert.Single(project.Document.Terrains.Single().SceneryPlacements).Id;
            project.Save(TestContext.Current.CancellationToken);
        }

        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Manifest, token));
            Assert.True(Find<ListBox>(window, "TerrainSceneryPlacements").IsEffectivelyVisible);
            Find<NumericUpDown>(window, "TerrainSceneryX").Value = -81;
            Find<NumericUpDown>(window, "TerrainSceneryY").Value = 67;
            Find<Button>(window, "ApplyTerrainScenery").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            TerrainSceneryPlacement placed = Assert.Single(window.Project.Document.Terrains.Single().SceneryPlacements);
            Assert.Equal(placementId, placed.Id);
            Assert.Equal((-81, 67), (placed.X, placed.Y));
            Assert.Equal((5, 8), (placed.SourceX, placed.SourceY));

            Find<Button>(window, "Undo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Equal((5, 8), (window.Project.Document.Terrains.Single().SceneryPlacements[0].X,
                window.Project.Document.Terrains.Single().SceneryPlacements[0].Y));
            Find<Button>(window, "Redo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Equal(-81, window.Project.Document.Terrains.Single().SceneryPlacements[0].X);

            Find<NumericUpDown>(window, "TerrainSceneryY").Value = 123;
            window.GetLogicalDescendants().OfType<Button>().Single(button => Equals(button.Content, "Save"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.False(window.Project.IsModified);
            Assert.Equal(123, window.Project.Document.Terrains.Single().SceneryPlacements[0].Y);
            Assert.Equal(terrainId, window.Project.Document.Terrains.Single().Id);
        }
        finally { window.Close(); }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        Assert.Equal(placementId, Assert.Single(reopened.Document.Terrains.Single().SceneryPlacements).Id);
        Assert.Equal((-81, 123), (reopened.Document.Terrains.Single().SceneryPlacements[0].X,
            reopened.Document.Terrains.Single().SceneryPlacements[0].Y));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task TerrainWithoutSceneryDoesNotShowPlacementInspector() =>
        s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            project.CreateTerrain("Ground", 9100, 1, 1, new TerrainCell(1, 0, 0));
            project.Save(TestContext.Current.CancellationToken);
        }

        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Manifest, token));
            Assert.False(Find<ListBox>(window, "TerrainSceneryPlacements").IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static TerrainImport CapturedMapWithAnimatedScenery()
    {
        TerrainImport capture = CreateCapturedTerrain();
        byte[] map = capture.DecodedSource!;
        GameMapCellGrid grid = GameMapCellGrid.Parse(map);
        GameMapSceneSections section = GameMapSceneSections.Parse(map, grid.BytesConsumed, grid.HeaderField0);
        int sceneryCount = grid.BytesConsumed + 4 + section.ExitMarkers.Count * 12;
        byte[] animation = new byte[4 + 0x1A0];
        BinaryPrimitives.WriteInt32LittleEndian(animation, 4);
        Encoding.Latin1.GetBytes("ani/puzzle.ani").CopyTo(animation, 4);
        Encoding.Latin1.GetBytes("Puzzle2").CopyTo(animation, 4 + 0x104);
        BinaryPrimitives.WriteInt32LittleEndian(animation.AsSpan(4 + 0x184), 5);
        BinaryPrimitives.WriteInt32LittleEndian(animation.AsSpan(4 + 0x188), 8);
        using MemoryStream stream = new();
        stream.Write(map.AsSpan(0, sceneryCount));
        using (BinaryWriter writer = new(stream, Encoding.Latin1, leaveOpen: true))
        {
            writer.Write(1);
        }
        stream.Write(animation);
        stream.Write(map.AsSpan(sceneryCount + 4));
        byte[] changed = stream.ToArray();
        GameMapCellGrid parsedGrid = GameMapCellGrid.Parse(changed);
        GameMapSceneSections parsed = GameMapSceneSections.Parse(changed,
            parsedGrid.BytesConsumed, parsedGrid.HeaderField0);
        return ClientTerrainImporter.Read(capture.MapDataId, capture.Source.MapPath, capture.Source.CatalogValue,
            parsedGrid, parsed, TestContext.Current.CancellationToken) with
        {
            DecodedSource = changed,
            PuzzleFiles = capture.PuzzleFiles,
            SceneFiles = capture.SceneFiles,
            AnimationFiles = capture.AnimationFiles,
            FrameFiles = capture.FrameFiles
        };
    }
}
