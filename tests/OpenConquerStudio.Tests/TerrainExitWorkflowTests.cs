using System.Collections.Immutable;
using System.Text.Json;
using OpenConquerStudio.Authoring;

namespace OpenConquerStudio.Tests;

public sealed class TerrainExitWorkflowTests
{
    [Fact]
    public void OrderedExitMarkersRoundTripAndExportWithoutDerivingDestinations()
    {
        using ProjectDirectory directory = new();
        Guid terrainId;
        TerrainExitMarker first = new(Guid.NewGuid(), -240, int.MaxValue, uint.MaxValue);
        TerrainExitMarker second = new(Guid.NewGuid(), 900, -700, 7);
        TerrainExitMarker moved = new(first.Id, int.MinValue, int.MaxValue, uint.MaxValue);
        using (StudioProject project = StudioProject.Create(directory.Project, "World"))
        {
            terrainId = project.CreateTerrain("Ground", 9500, 2, 1, new TerrainCell(1, 0, 0));
            project.EditTerrainExits(terrainId, [first, second]);
            project.Save(TestContext.Current.CancellationToken);
            ProjectDocument saved = project.Document;

            project.EditTerrainExits(terrainId, new[] { second, moved });
            Assert.Equal(new[] { second, moved }, project.Document.Terrains.Single().Exits.ToArray());
            project.Undo();
            Assert.Same(saved, project.Document);
            Assert.False(project.IsModified);
            project.Redo();
            project.Save(TestContext.Current.CancellationToken);
        }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        TerrainAsset terrain = Assert.Single(reopened.Document.Terrains);
        Assert.Equal(terrainId, terrain.Id);
        Assert.Equal(new[] { second, moved }, terrain.Exits.ToArray());
        string output = directory.PathFor("source.json");
        TerrainSourceExporter.Export(reopened, terrainId, output, TestContext.Current.CancellationToken);
        using JsonDocument json = JsonDocument.Parse(File.ReadAllBytes(output));
        JsonElement.ArrayEnumerator exits = json.RootElement.GetProperty("exits").EnumerateArray();
        Assert.Equal(2, exits.Count());
        Assert.Equal((900, -700, 7u), (exits.First().GetProperty("x").GetInt32(),
            exits.First().GetProperty("y").GetInt32(), exits.First().GetProperty("passwayIndex").GetUInt32()));
        Assert.Equal((int.MinValue, int.MaxValue, uint.MaxValue), (exits.Last().GetProperty("x").GetInt32(),
            exits.Last().GetProperty("y").GetInt32(), exits.Last().GetProperty("passwayIndex").GetUInt32()));
        Assert.Equal(["x", "y", "passwayIndex"],
            exits.First().EnumerateObject().Select(p => p.Name).ToArray());
        Assert.DoesNotContain(first.Id.ToString(), File.ReadAllText(output), StringComparison.Ordinal);
    }

    [Fact]
    public void IdenticalEditsPreserveSavedStateAndRedo()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "World");
        Guid terrain = project.CreateTerrain("Ground", 9600, 2, 1, new TerrainCell(0, 0, 0));
        TerrainExitMarker marker = new(Guid.NewGuid(), -1, 1, 0);
        project.EditTerrainExits(terrain, [marker]);
        project.Save(TestContext.Current.CancellationToken);
        ProjectDocument saved = project.Document;
        project.Rename("Temporary");
        project.Undo();
        Assert.True(project.CanRedo);

        project.EditTerrainExits(terrain, [marker]);
        Assert.Same(saved, project.Document);
        Assert.True(project.CanRedo);
        Assert.False(project.IsModified);
        project.Redo();
        Assert.Equal("Temporary", project.Document.Name);
    }

    [Fact]
    public void RejectsInvalidOrExcessiveMarkerListsBeforePublishingAnyRevision()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "World");
        Guid first = project.CreateTerrain("First", 9700, 2, 1, new TerrainCell(0, 0, 0));
        Guid second = project.CreateTerrain("Second", 9701, 1, 1, new TerrainCell(0, 0, 0));
        TerrainExitMarker marker = new(Guid.NewGuid(), 0, 0, 1);
        project.EditTerrainExits(first, [marker]);
        project.Save(TestContext.Current.CancellationToken);
        ProjectDocument saved = project.Document;

        Assert.Throws<InvalidDataException>(() => project.EditTerrainExits(first, default(ImmutableArray<TerrainExitMarker>)));
        Assert.Throws<InvalidDataException>(() => project.EditTerrainExits(first, [marker, marker]));
        Assert.Throws<InvalidDataException>(() => project.EditTerrainExits(second, [marker]));
        Assert.Throws<InvalidDataException>(() => project.EditTerrainExits(first, [marker with { Id = Guid.Empty }]));
        ImmutableArray<TerrainExitMarker> large = [.. Enumerable.Range(0, ProjectDocument.MaximumTerrainExitMarkers + 1)
            .Select(index => new TerrainExitMarker(Guid.NewGuid(), index, -index, (uint)index))];
        Assert.Throws<InvalidDataException>(() => project.EditTerrainExits(first, large));

        Assert.Same(saved, project.Document);
        Assert.False(project.IsModified);
        Assert.Equal(new[] { marker }, project.Document.Terrains.Single(a => a.Id == first).Exits.ToArray());
    }
}
