using System.Text.Json;
using OpenConquerStudio.Authoring;

namespace OpenConquerStudio.Tests;

public sealed class WorldDefinitionWorkflowTests
{
    [Fact]
    public void WorldsBindDistinctMapIdsToSharedTerrainAndPreserveFullFlags()
    {
        using ProjectDirectory directory = new();
        Guid original;
        Guid shared;
        Guid other;
        Guid terrainId;
        using (StudioProject project = StudioProject.Create(directory.Project, "Worlds"))
        {
            terrainId = project.CreateTerrain("Ground", 1010, 2, 1, new TerrainCell(12, 1, -5));
            original = project.CreateWorld("First", 1002, terrainId, ulong.MaxValue);
            shared = project.CreateWorld("Second", 1003, terrainId, 0);
            other = project.CreateTerrain("Second terrain", 1079, 1, 1, new TerrainCell(0, 0, 0));
            Assert.Equal(new[] { original, shared }.Order().ToArray(),
                project.Document.Dependents(terrainId).Select(info => info.Id).Order().ToArray());
            Assert.Equal(new[] { terrainId }, project.Document.Dependencies(original).ToArray());
            Assert.Throws<InvalidDataException>(() => project.Delete(terrainId));
            project.Save(TestContext.Current.CancellationToken);

            WorldAsset originalRecord = project.Document.Worlds.Single(a => a.Id == original);
            project.EditWorld(originalRecord with { Name = "First renamed", TerrainId = other, Flags = 1UL << 40 });
            Assert.True(project.IsModified);
            project.Undo();
            Assert.False(project.IsModified);
            Assert.Equal(ulong.MaxValue, project.Document.Worlds.Single(a => a.Id == original).Flags);
            project.Redo();
            project.Save(TestContext.Current.CancellationToken);
        }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        Assert.Equal(2, reopened.Document.Worlds.Length);
        Assert.Equal(new[] { 1002u, 1003u }, reopened.Document.Worlds.Select(world => world.MapId).Order().ToArray());
        WorldAsset first = reopened.Document.Worlds.Single(world => world.Id == original);
        Assert.Equal("First renamed", first.Name);
        Assert.Equal(other, first.TerrainId);
        Assert.Equal(1UL << 40, first.Flags);
        Assert.Equal(terrainId, reopened.Document.Worlds.Single(world => world.Id == shared).TerrainId);
        Assert.Equal(0UL, reopened.Document.Worlds.Single(world => world.Id == shared).Flags);
        reopened.Delete(first.Id);
        Assert.Single(reopened.Document.Worlds);
    }

    [Fact]
    public void IdenticalWorldEditDoesNotDirtyProjectOrDiscardRedo()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Worlds");
        Guid terrain = project.CreateTerrain("Ground", 2020, 1, 1, new TerrainCell(0, 0, 0));
        Guid id = project.CreateWorld("World", 2001, terrain, ulong.MaxValue);
        project.Save(TestContext.Current.CancellationToken);
        ProjectDocument saved = project.Document;
        WorldAsset world = Assert.Single(saved.Worlds);
        project.Rename("Changed");
        project.Undo();
        Assert.True(project.CanRedo);
        project.EditWorld(world);
        Assert.Same(saved, project.Document);
        Assert.True(project.CanRedo);
        Assert.False(project.IsModified);
    }

    [Fact]
    public void DuplicateWorldMapIdsUnknownTerrainsAndInvalidReferencesFailBeforeMutation()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Worlds");
        Guid terrain = project.CreateTerrain("Ground", 1010, 1, 1, new TerrainCell(0, 0, 0));
        Guid id = project.CreateWorld("Known", 1002, terrain, 0);
        project.Save(TestContext.Current.CancellationToken);
        ProjectDocument saved = project.Document;

        Assert.Throws<InvalidDataException>(() => project.CreateWorld("Duplicate", 1002, terrain, 0));
        Assert.Throws<InvalidDataException>(() => project.CreateWorld("Missing", 1003, Guid.NewGuid(), 0));
        Assert.Throws<InvalidDataException>(() => project.CreateWorld("Invalid", 0, terrain, 0));
        Assert.Throws<InvalidDataException>(() => project.CreateWorld("", 1004, terrain, 0));
        Assert.Throws<InvalidDataException>(() => project.EditWorld(saved.Worlds.Single() with { MapId = 0 }));
        Assert.Throws<InvalidDataException>(() => project.EditWorld(saved.Worlds.Single() with { TerrainId = Guid.Empty }));
        Assert.Throws<InvalidDataException>(() => project.Delete(terrain));
        Assert.Same(saved, project.Document);
        Assert.False(project.IsModified);
        Assert.Equal(id, Assert.Single(project.Document.Worlds).Id);
    }

    [Fact]
    public void ExportPreservesIdentitySeparationAndCanonical64BitSourceSchema()
    {
        using ProjectDirectory directory = new();
        Guid first;
        using (StudioProject project = StudioProject.Create(directory.Project, "Worlds"))
        {
            Guid terrain = project.CreateTerrain("Ground", 1010, 2, 1, new TerrainCell(1, 0, 0));
            first = project.CreateWorld("Later", 2003, terrain, ulong.MaxValue);
            project.CreateWorld("Earlier", 2002, terrain, 274877906960ul);
            project.Save(TestContext.Current.CancellationToken);
            WorldDefinitionSourceExporter.Export(project, directory.PathFor("first.json"), TestContext.Current.CancellationToken);
        }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        WorldDefinitionSourceExporter.Export(reopened, directory.PathFor("second.json"), TestContext.Current.CancellationToken);
        byte[] original = File.ReadAllBytes(directory.PathFor("first.json"));
        Assert.Equal(original, File.ReadAllBytes(directory.PathFor("second.json")));
        using JsonDocument parsed = JsonDocument.Parse(original);
        JsonElement root = parsed.RootElement;
        Assert.Equal(new[] { "formatVersion", "maps" }, root.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(1, root.GetProperty("formatVersion").GetInt32());
        JsonElement[] maps = root.GetProperty("maps").EnumerateArray().ToArray();
        Assert.Equal(2, maps.Length);
        Assert.Equal(new[] { "mapId", "mapDataId", "flags" }, maps[0].EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal((2002u, 1010u, 274877906960ul), (maps[0].GetProperty("mapId").GetUInt32(),
            maps[0].GetProperty("mapDataId").GetUInt32(), maps[0].GetProperty("flags").GetUInt64()));
        Assert.Equal((2003u, 1010u, ulong.MaxValue), (maps[1].GetProperty("mapId").GetUInt32(),
            maps[1].GetProperty("mapDataId").GetUInt32(), maps[1].GetProperty("flags").GetUInt64()));
        Assert.DoesNotContain(first.ToString(), File.ReadAllText(directory.PathFor("first.json")), StringComparison.Ordinal);
        reopened.EditWorld(reopened.Document.Worlds.Single(world => world.Id == first) with { Name = "Display only" });
        reopened.Save(TestContext.Current.CancellationToken);
        WorldDefinitionSourceExporter.Export(reopened, directory.PathFor("third.json"), TestContext.Current.CancellationToken);
        Assert.Equal(original, File.ReadAllBytes(directory.PathFor("third.json")));
    }

    [Fact]
    public void ExportRejectsUncommittedProjectsUnknownDestinationsAndCancellation()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Worlds");
        string destination = directory.PathFor("source.json");
        Assert.Throws<InvalidDataException>(() => WorldDefinitionSourceExporter.Export(project, destination));
        Guid terrain = project.CreateTerrain("Ground", 1010, 1, 1, new TerrainCell(0, 0, 0));
        project.CreateWorld("World", 1002, terrain, 0);
        Assert.Throws<InvalidOperationException>(() => WorldDefinitionSourceExporter.Export(project, destination));
        project.Save(TestContext.Current.CancellationToken);
        using CancellationTokenSource canceled = new();
        canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => WorldDefinitionSourceExporter.Export(project, destination, canceled.Token));
        Assert.False(File.Exists(destination));
        WorldDefinitionSourceExporter.Export(project, destination, TestContext.Current.CancellationToken);
        byte[] saved = File.ReadAllBytes(destination);
        Assert.Throws<IOException>(() => WorldDefinitionSourceExporter.Export(project, destination));
        Assert.Equal(saved, File.ReadAllBytes(destination));
    }

    [Theory]
    [InlineData("0xFFFFFFFFFFFFFFFF", ulong.MaxValue)]
    [InlineData("18446744073709551615", ulong.MaxValue)]
    [InlineData("0x4000000000", 274877906944ul)]
    [InlineData("0", 0UL)]
    public void WorldFlagsAcceptEntireUnsignedRangeInDecimalAndHex(string input, ulong expected) =>
        Assert.Equal(expected, WorldFlagValue.Parse(input));

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("18446744073709551616")]
    [InlineData("0x10000000000000000")]
    [InlineData("not-flags")]
    public void InvalidWorldFlagInputsAreRejected(string input) =>
        Assert.Throws<InvalidDataException>(() => WorldFlagValue.Parse(input));
}
