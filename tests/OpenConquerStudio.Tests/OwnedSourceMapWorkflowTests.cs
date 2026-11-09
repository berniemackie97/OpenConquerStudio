using System.Text.Json;
using OpenConquer.Content.Map;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.ConquerImport;

namespace OpenConquerStudio.Tests;

public sealed class OwnedSourceMapWorkflowTests
{
    [Fact]
    public void CapturedMapRetainsExactBytesAndOrderedDependenciesAcrossSaveReopenAndEdits()
    {
        using ProjectDirectory directory = new();
        byte[] decoded = ClientTerrainImporterTests.MapSource();
        GameMapCellGrid grid = GameMapCellGrid.Parse(decoded);
        GameMapSceneSections scene = GameMapSceneSections.Parse(decoded, grid.BytesConsumed, grid.HeaderField0);
        TerrainImport input = ClientTerrainImporter.Read(9100, "maps/ground.dmap", 42, grid, scene,
            TestContext.Current.CancellationToken) with
        {
            DecodedSource = decoded
        };
        Guid id;
        SourceMapInventory original;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            id = project.ImportTerrain(input, "Ground");
            TerrainAsset terrain = Assert.Single(project.Document.Terrains);
            Assert.NotNull(terrain.Source?.SnapshotDigest);
            Assert.Equal(decoded.Length, terrain.Source.SnapshotLength);
            original = project.ReadSourceMapInventory(id);
            Assert.Equal(1, original.FormatVersion);
            Assert.Equal(9100u, original.MapDataId);
            Assert.Equal(1, original.ExitCount);
            Assert.Equal(10, original.Scenery.Single().TypeCode);
            SourceMapOverlayGroup overlay = Assert.Single(original.Overlays);
            Assert.Equal(0, overlay.InsertIndex);
            Assert.Equal((31, 37), (overlay.HeaderField0, overlay.HeaderField1));
            Assert.Equal(2, original.References.Length);
            Assert.Equal(SourceMapReferenceKind.GroundPuzzle, original.References[0].Kind);
            Assert.Equal("puzzles/ground.pul", original.References[0].ResourcePath);
            Assert.Equal(SourceMapReferenceKind.PositionedPuzzle, original.References[1].Kind);
            Assert.Equal("puzzles/layer.pul", original.References[1].ResourcePath);
            project.Save(TestContext.Current.CancellationToken);

            project.EditTerrainCell(id, 1, 0, new TerrainCell(44, 2, -55), "Edited ground");
            Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(project.ReadSourceMapInventory(id)));
            project.Save(TestContext.Current.CancellationToken);
        }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        Assert.Equal("Edited ground", Assert.Single(reopened.Document.Terrains).Name);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(reopened.ReadSourceMapInventory(id)));
        string output = directory.PathFor("inventory.json");
        SourceMapInventory.Export(reopened, id, output, TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(output));
        Assert.Equal("puzzles/layer.pul",
            document.RootElement.GetProperty("references")[1].GetProperty("resourcePath").GetString());
        string digest = reopened.Document.Terrains.Single().Source!.SnapshotDigest!;
        byte[] bytes = File.ReadAllBytes(directory.PathFor("project", "Inputs", digest + ".ocmapsource"));
        Assert.Equal(decoded, bytes);
    }

    [Fact]
    public void CorruptSnapshotIsRejectedBeforeOpeningTheProject()
    {
        using ProjectDirectory directory = new();
        byte[] decoded = ClientTerrainImporterTests.MapSource();
        GameMapCellGrid grid = GameMapCellGrid.Parse(decoded);
        GameMapSceneSections scene = GameMapSceneSections.Parse(decoded, grid.BytesConsumed, grid.HeaderField0);
        string digest;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            TerrainImport source = ClientTerrainImporter.Read(9200, "maps/ground.dmap", 42, grid, scene,
                TestContext.Current.CancellationToken) with
            {
                DecodedSource = decoded
            };
            Guid id = project.ImportTerrain(source, "Ground");
            digest = project.Document.Terrains.Single().Source!.SnapshotDigest!;
            project.Save(TestContext.Current.CancellationToken);
            Assert.Equal(9200u, project.ReadSourceMapInventory(id).MapDataId);
        }

        string stored = directory.PathFor("project", "Inputs", digest + ".ocmapsource");
        byte[] damaged = File.ReadAllBytes(stored);
        damaged[damaged.Length / 2] ^= 0xFF;
        File.WriteAllBytes(stored, damaged);
        Assert.Throws<InvalidDataException>(() => StudioProject.Open(directory.Project,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void InconsistentCaptureRejectsMutationAndLegacyExtractionHasNoInventedSnapshot()
    {
        using ProjectDirectory directory = new();
        byte[] decoded = ClientTerrainImporterTests.MapSource();
        GameMapCellGrid grid = GameMapCellGrid.Parse(decoded);
        GameMapSceneSections scene = GameMapSceneSections.Parse(decoded, grid.BytesConsumed, grid.HeaderField0);
        TerrainImport input = ClientTerrainImporter.Read(9300, "maps/ground.dmap", 42, grid, scene,
            TestContext.Current.CancellationToken) with
        {
            DecodedSource = decoded
        };
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        ProjectDocument initial = project.Document;
        byte[] damaged = (byte[])decoded.Clone();
        damaged[276] ^= 0x01;
        Assert.Throws<InvalidDataException>(() => project.ImportTerrain(input with
        {
            DecodedSource = damaged
        }, "Damaged"));
        Assert.Same(initial, project.Document);
        Assert.False(project.IsModified);

        Guid id = project.ImportTerrain(input with
        {
            DecodedSource = null
        }, "Legacy source");
        project.Save(TestContext.Current.CancellationToken);
        Assert.Null(project.Document.Terrains.Single().Source!.SnapshotDigest);
        Assert.Throws<InvalidDataException>(() => project.ReadSourceMapInventory(id));
        Assert.Throws<InvalidDataException>(() => project.RefreshTerrain(id, input with
        {
            DecodedSource = damaged
        }));
        Assert.Single(project.Document.Terrains);
    }

    [Fact]
    public void SnapshotRefreshIsAtomicAndPreservesUndoRedoAndIdentity()
    {
        using ProjectDirectory directory = new();
        byte[] decoded = ClientTerrainImporterTests.MapSource();
        GameMapCellGrid grid = GameMapCellGrid.Parse(decoded);
        GameMapSceneSections scene = GameMapSceneSections.Parse(decoded, grid.BytesConsumed, grid.HeaderField0);
        TerrainImport input = ClientTerrainImporter.Read(9400, "maps/ground.dmap", 42, grid, scene,
            TestContext.Current.CancellationToken) with
        {
            DecodedSource = decoded
        };
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid id = project.ImportTerrain(input, "Ground");
        project.Save(TestContext.Current.CancellationToken);
        ProjectDocument saved = project.Document;
        project.RefreshTerrain(id, input with
        {
            Exits = [new TerrainExitMarker(Guid.NewGuid(), -2, 400, uint.MaxValue)]
        });
        Assert.Same(saved, project.Document);
        Assert.False(project.IsModified);

        project.PaintTerrainRegion(id, new TerrainRegion(0, 0, 0, 0), new TerrainCell(5, 8, -1), "Ground");
        project.Undo();
        Assert.Same(saved, project.Document);
        Assert.True(project.CanRedo);
        Assert.Throws<InvalidDataException>(() => project.RefreshTerrain(id, input with
        {
            DecodedSource = null
        }));
        Assert.True(project.CanRedo);
        Assert.Same(saved, project.Document);
    }
}
