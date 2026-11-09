using System.Collections.Immutable;
using System.Text.Json;
using OpenConquerStudio.Authoring;

namespace OpenConquerStudio.Tests;

public sealed class SourceTerrainWorkflowTests
{
    private static readonly TerrainSource s_origin = new("maps/ground.dmap", "puzzles/ground.pul",
        101, 1005, 42, 2, 1);

    [Fact]
    public void ImportRefreshUndoAndReopenPreserveWorldBindingAndMatchingMarkerIdentities()
    {
        using ProjectDirectory directory = new();
        Guid firstMarker = Guid.NewGuid();
        Guid secondMarker = Guid.NewGuid();
        TerrainImport initial = new(9100, 3, 2,
            [new(1, 0, -1), new(2, 1, -2), new(3, 2, -3),
             new(4, 3, -4), new(5, 4, -5), new(6, 5, -6)],
            [new(firstMarker, -3, 40, uint.MaxValue), new(secondMarker, 7, 9, 8)], s_origin);
        Guid terrainId;
        Guid worldId;

        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            terrainId = project.ImportTerrain(initial, "Ground");
            worldId = project.CreateWorld("Dungeon", 9300, terrainId, 1UL << 42);
            project.Save(TestContext.Current.CancellationToken);
            Assert.Equal(initial.Cells[5], project.ReadTerrainCell(terrainId, 2, 1));

            TerrainImport changed = initial with
            {
                Cells = [new(99, ushort.MaxValue, short.MinValue), .. initial.Cells.AsSpan()[1..].ToArray()],
                Exits = [new(Guid.NewGuid(), 7, 9, 8), new(Guid.NewGuid(), -3, 40, uint.MaxValue)]
            };
            project.RefreshTerrain(terrainId, changed);
            TerrainAsset refreshed = Assert.Single(project.Document.Terrains);
            Assert.Equal(terrainId, refreshed.Id);
            Assert.Equal([secondMarker, firstMarker], refreshed.Exits.Select(marker => marker.Id).ToArray());
            Assert.Equal(new TerrainCell(99, ushort.MaxValue, short.MinValue),
                project.ReadTerrainCell(terrainId, 0, 0));
            Assert.Equal(terrainId, Assert.Single(project.Document.Worlds).TerrainId);
            Assert.Equal(worldId, Assert.Single(project.Document.Worlds).Id);

            project.Undo();
            Assert.False(project.IsModified);
            Assert.Equal(initial.Cells[0], project.ReadTerrainCell(terrainId, 0, 0));
            Assert.Equal([firstMarker, secondMarker], project.Document.Terrains.Single().Exits.Select(m => m.Id).ToArray());
            project.Redo();
            project.Save(TestContext.Current.CancellationToken);
            Assert.False(project.IsModified);
        }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        TerrainAsset actual = Assert.Single(reopened.Document.Terrains);
        Assert.Equal(terrainId, actual.Id);
        Assert.Equal(s_origin, actual.Source);
        Assert.Equal(new TerrainCell(99, ushort.MaxValue, short.MinValue),
            reopened.ReadTerrainCell(terrainId, 0, 0));
        Assert.Equal(terrainId, Assert.Single(reopened.Document.Worlds).TerrainId);
        Assert.Equal([secondMarker, firstMarker], actual.Exits.Select(m => m.Id).ToArray());
        using JsonDocument index = JsonDocument.Parse(File.ReadAllBytes(directory.Manifest));
        string digest = index.RootElement.GetProperty("resources").EnumerateArray().Single(resource =>
            resource.GetProperty("id").GetGuid() == terrainId).GetProperty("sha256").GetString()!;
        string revision = directory.PathFor("project", "Assets", terrainId.ToString("N"), digest + ".ocasset.json");
        using JsonDocument metadata = JsonDocument.Parse(File.ReadAllBytes(revision));
        Assert.Equal(2, metadata.RootElement.GetProperty("schemaVersion").GetInt32());

        string exported = directory.PathFor("terrain-source.json");
        TerrainSourceExporter.Export(reopened, terrainId, exported, TestContext.Current.CancellationToken);
        using JsonDocument source = JsonDocument.Parse(File.ReadAllBytes(exported));
        Assert.Equal(-3, source.RootElement.GetProperty("exits")[1].GetProperty("x").GetInt32());
        Assert.Equal(uint.MaxValue, source.RootElement.GetProperty("exits")[1].GetProperty("passwayIndex").GetUInt32());
    }

    [Fact]
    public void IdenticalRefreshDoesNotLoseRedoOrChangeSavedRevision()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid markerId = Guid.NewGuid();
        TerrainImport imported = new(9300, 2, 1, [new(1, 2, 3), new(4, 5, 6)],
            [new(markerId, -1, 2, 3)], s_origin);
        Guid terrainId = project.ImportTerrain(imported, "Imported");
        project.Save(TestContext.Current.CancellationToken);
        ProjectDocument saved = project.Document;
        byte[] index = File.ReadAllBytes(directory.Manifest);
        project.Rename("Temporary");
        project.Undo();
        Assert.True(project.CanRedo);

        project.RefreshTerrain(terrainId, imported with
        {
            Exits = [new TerrainExitMarker(Guid.NewGuid(), -1, 2, 3)]
        });
        Assert.Same(saved, project.Document);
        Assert.True(project.CanRedo);
        Assert.False(project.IsModified);
        project.Save(TestContext.Current.CancellationToken);
        Assert.Equal(index, File.ReadAllBytes(directory.Manifest));
    }

    [Fact]
    public void UnsafeSourceOrInvalidShapeNeverMutatesProject()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid originalId = project.CreateTerrain("Authored", 1000, 1, 1, new TerrainCell(0, 0, 0));
        project.Save(TestContext.Current.CancellationToken);
        ProjectDocument saved = project.Document;
        TerrainImport candidate = new(2000, 2, 1, [new(1, 2, 3), new(4, 5, 6)],
            [new(Guid.NewGuid(), -1, 9, uint.MaxValue)], s_origin);

        Assert.Throws<InvalidDataException>(() => project.ImportTerrain(candidate with
        {
            Source = s_origin with { MapPath = "../outside.dmap" }
        }, "Terrain"));
        Assert.Throws<InvalidDataException>(() => project.ImportTerrain(candidate with
        {
            Cells = [new TerrainCell(1, 2, 3)]
        }, "Terrain"));
        Assert.Throws<InvalidDataException>(() => project.ImportTerrain(candidate with
        {
            MapDataId = 1000
        }, "Duplicate"));
        Assert.Throws<InvalidDataException>(() => project.RefreshTerrain(originalId, candidate));
        Assert.Throws<InvalidDataException>(() => project.ImportTerrain(candidate with
        {
            Source = s_origin with { SceneryCount = -1 }
        }, "Terrain"));
        Assert.Same(saved, project.Document);
        Assert.False(project.IsModified);
        Assert.Single(project.Document.Terrains);
    }

    [Fact]
    public void LegacyTerrainResourcesKeepVersionOneAndCanStillBeEdited()
    {
        using ProjectDirectory directory = new();
        Guid id;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            id = project.CreateTerrain("Ground", 9100, 1, 1, new TerrainCell(1, 2, 3));
            project.Save(TestContext.Current.CancellationToken);
        }

        string revision = Assert.Single(Directory.GetFiles(directory.PathFor("project", "Assets", id.ToString("N")), "*.ocasset.json"));
        using (JsonDocument metadata = JsonDocument.Parse(File.ReadAllBytes(revision)))
        {
            Assert.Equal(1, metadata.RootElement.GetProperty("schemaVersion").GetInt32());
        }
        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        Assert.Null(Assert.Single(reopened.Document.Terrains).Source);
        reopened.EditTerrainCell(id, 0, 0, new TerrainCell(4, 5, 6), "Ground");
        reopened.Save(TestContext.Current.CancellationToken);
        Assert.Equal(new TerrainCell(4, 5, 6), reopened.ReadTerrainCell(id, 0, 0));
    }
}
