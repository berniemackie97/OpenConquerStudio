using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using OpenConquer.Content.Map;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.ConquerImport;

namespace OpenConquerStudio.Tests;

public sealed class SourceSceneryPlacementWorkflowTests
{
    [Fact]
    public void KnownSceneryPositionsBecomeStableAuthoredPlacementsWhileOpaqueRecordsRemainSourceOnly()
    {
        using ProjectDirectory directory = new();
        TerrainImport capture = SourceWithScenery();
        Guid id;
        Guid[] placements;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            id = project.ImportTerrain(capture, "Ground");
            TerrainAsset asset = Assert.Single(project.Document.Terrains);
            Assert.Equal(3, asset.Source!.SceneryCount);
            Assert.Equal([1, 4], asset.SceneryPlacements.Select(a => a.TypeCode).ToArray());
            Assert.Equal([0, 1], asset.SceneryPlacements.Select(a => a.SourceIndex).ToArray());
            Assert.Equal([(17, 29), (41, 42)],
                asset.SceneryPlacements.Select(a => (a.X, a.Y)).ToArray());
            placements = asset.SceneryPlacements.Select(a => a.Id).ToArray();

            ImmutableArray<TerrainSceneryPlacement> moved = [.. asset.SceneryPlacements.Select(a =>
                a.TypeCode == 1 ? a with { X = -123, Y = 456 } : a)];
            project.PaintTerrainRegion(id, new TerrainRegion(0, 0, 0, 0),
                project.ReadTerrainCell(id, 0, 0), "Edited ground", sceneryPlacements: moved);
            Assert.Equal("Edited ground", project.Document.Terrains.Single().Name);
            Assert.Equal((-123, 456), (project.Document.Terrains.Single().SceneryPlacements[0].X,
                project.Document.Terrains.Single().SceneryPlacements[0].Y));
            project.Undo();
            Assert.Equal("Ground", project.Document.Terrains.Single().Name);
            Assert.Equal(17, project.Document.Terrains.Single().SceneryPlacements[0].X);
            project.Redo();
            project.Save(TestContext.Current.CancellationToken);
        }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        TerrainAsset terrain = Assert.Single(reopened.Document.Terrains);
        Assert.Equal(placements, terrain.SceneryPlacements.Select(a => a.Id).ToArray());
        Assert.Equal((-123, 456), (terrain.SceneryPlacements[0].X, terrain.SceneryPlacements[0].Y));
        Assert.Equal((17, 29), (terrain.SceneryPlacements[0].SourceX, terrain.SceneryPlacements[0].SourceY));
        SourceMapInventory inventory = reopened.ReadSourceMapInventory(id);
        Assert.Equal(3, inventory.Scenery.Length);
        Assert.Equal(10, inventory.Scenery[2].TypeCode);
        Assert.Equal(ProjectFiles.Hash(capture.DecodedSource!), terrain.Source!.SnapshotDigest);
    }

    [Fact]
    public void RefreshRetainsAuthoredCoordinatesAndIdsAcrossSourceReorder()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid id = project.ImportTerrain(SourceWithScenery(), "Ground");
        TerrainSceneryPlacement[] original = project.Document.Terrains.Single().SceneryPlacements.ToArray();
        project.EditTerrainSceneryPlacement(id, original[0].Id, -91, 777);
        project.Save(TestContext.Current.CancellationToken);
        ProjectDocument saved = project.Document;

        project.RefreshTerrain(id, SourceWithScenery(reorder: true, firstX: 55, firstY: 88));
        TerrainSceneryPlacement[] placements = project.Document.Terrains.Single().SceneryPlacements.ToArray();
        Assert.Equal([4, 1], placements.Select(p => p.TypeCode).ToArray());
        Assert.Equal(original[1].Id, placements[0].Id);
        Assert.Equal(original[0].Id, placements[1].Id);
        Assert.Equal((-91, 777), (placements[1].X, placements[1].Y));
        Assert.Equal((55, 88), (placements[1].SourceX, placements[1].SourceY));
        Assert.Equal((41, 42), (placements[0].X, placements[0].Y));
        project.Undo();
        Assert.Same(saved, project.Document);
        project.Redo();
        project.Save(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void SourceCoordinateUpdatesFollowSourceWhenThereWasNoAuthoredOverride()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid id = project.ImportTerrain(SourceWithScenery(), "Ground");
        Guid originalId = project.Document.Terrains.Single().SceneryPlacements[0].Id;
        project.Save(TestContext.Current.CancellationToken);
        project.RefreshTerrain(id, SourceWithScenery(firstX: -8, firstY: 19));
        TerrainSceneryPlacement placement = project.Document.Terrains.Single().SceneryPlacements[0];
        Assert.Equal(originalId, placement.Id);
        Assert.Equal((-8, 19), (placement.X, placement.Y));
        Assert.Equal((-8, 19), (placement.SourceX, placement.SourceY));
    }

    [Fact]
    public void NoOpMoveAndUnchangedSourceRefreshPreserveRedo()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        TerrainImport capture = SourceWithScenery();
        Guid id = project.ImportTerrain(capture, "Ground");
        Guid placementId = project.Document.Terrains.Single().SceneryPlacements[0].Id;
        project.Save(TestContext.Current.CancellationToken);
        ProjectDocument saved = project.Document;
        project.Rename("Temporary");
        project.Undo();
        Assert.True(project.CanRedo);
        project.EditTerrainSceneryPlacement(id, placementId, 17, 29);
        Assert.Same(saved, project.Document);
        project.RefreshTerrain(id, capture);
        Assert.Same(saved, project.Document);
        Assert.False(project.IsModified);
        Assert.True(project.CanRedo);
    }

    [Fact]
    public void InvalidOrFabricatedSourcePlacementEditsAreRejectedAtomically()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid id = project.ImportTerrain(SourceWithScenery(), "Ground");
        TerrainAsset asset = project.Document.Terrains.Single();
        ProjectDocument saved = project.Document;
        Assert.Throws<InvalidDataException>(() =>
            project.EditTerrainSceneryPlacement(id, Guid.NewGuid(), 1, 2));
        Assert.Throws<InvalidDataException>(() => project.PaintTerrainRegion(id,
            new TerrainRegion(0, 0, 0, 0), project.ReadTerrainCell(id, 0, 0), "Bad",
            sceneryPlacements: asset.SceneryPlacements.SetItem(0,
                asset.SceneryPlacements[0] with { SourceIndex = 1 })));
        Assert.Throws<InvalidDataException>(() => project.PaintTerrainRegion(id,
            new TerrainRegion(0, 0, 0, 0), project.ReadTerrainCell(id, 0, 0), "Bad",
            sceneryPlacements: asset.SceneryPlacements.RemoveAt(0)));
        Assert.Same(saved, project.Document);
        Assert.False(project.IsModified == saved.SchemaVersion);
    }

    internal static TerrainImport SourceWithScenery(bool reorder = false, int firstX = 17, int firstY = 29)
    {
        TerrainImport capture = OwnedMapVisualInputsTests.Input();
        byte[] map = capture.DecodedSource!;
        GameMapCellGrid grid = GameMapCellGrid.Parse(map);
        GameMapSceneSections original = GameMapSceneSections.Parse(map, grid.BytesConsumed, grid.HeaderField0);
        int countOffset = grid.BytesConsumed + sizeof(int) + original.ExitMarkers.Count * 12;
        const int FirstRecordLength = 4 + 0x10C;
        byte[] first = map.AsSpan(countOffset + 4, FirstRecordLength).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(first.AsSpan(4 + 0x104), firstX);
        BinaryPrimitives.WriteInt32LittleEndian(first.AsSpan(4 + 0x108), firstY);

        byte[] animated = new byte[4 + 0x1A0];
        BinaryPrimitives.WriteInt32LittleEndian(animated, 4);
        Encoding.Latin1.GetBytes("ani/scenery.ani").CopyTo(animated, 4);
        Encoding.Latin1.GetBytes("SceneTile").CopyTo(animated, 4 + 0x104);
        BinaryPrimitives.WriteInt32LittleEndian(animated.AsSpan(4 + 0x184), 41);
        BinaryPrimitives.WriteInt32LittleEndian(animated.AsSpan(4 + 0x188), 42);
        byte[] unknown = new byte[4 + 0x48];
        BinaryPrimitives.WriteInt32LittleEndian(unknown, 10);
        unknown[10] = 33;
        using MemoryStream output = new();
        output.Write(map.AsSpan(0, countOffset));
        using (BinaryWriter writer = new(output, Encoding.Latin1, leaveOpen: true))
        {
            writer.Write(3);
        }
        output.Write(reorder ? animated : first);
        output.Write(reorder ? first : animated);
        output.Write(unknown);
        output.Write(map.AsSpan(countOffset + 4 + FirstRecordLength));
        byte[] changed = output.ToArray();
        GameMapCellGrid parsedGrid = GameMapCellGrid.Parse(changed);
        GameMapSceneSections parsedScene = GameMapSceneSections.Parse(changed,
            parsedGrid.BytesConsumed, parsedGrid.HeaderField0);
        return ClientTerrainImporter.Read(capture.MapDataId, capture.Source.MapPath,
            capture.Source.CatalogValue, parsedGrid, parsedScene, TestContext.Current.CancellationToken) with
        {
            DecodedSource = changed,
            PuzzleFiles = capture.PuzzleFiles,
            SceneFiles = capture.SceneFiles,
            AnimationFiles = capture.AnimationFiles,
            FrameFiles = capture.FrameFiles
        };
    }
}
