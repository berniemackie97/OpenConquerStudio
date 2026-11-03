using System.Text.Json;
using OpenConquerStudio.Authoring;

namespace OpenConquerStudio.Tests;

public sealed class TerrainSourceExportTests
{
    [Fact]
    public void ExportMatchesServerAuthoringSourceContract()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "World");
        Guid terrainId = project.CreateTerrain("Field", 9100, 3, 2, new TerrainCell(12, 0, -30));
        project.EditTerrainCell(terrainId, 2, 1, new TerrainCell(ushort.MaxValue, 5, short.MinValue), "Field");
        project.Save(TestContext.Current.CancellationToken);

        string destination = directory.PathFor("source.json");
        TerrainSourceExporter.Export(project, terrainId, destination, TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(destination));
        JsonElement root = document.RootElement;
        Assert.Equal(["formatVersion", "mapDataId", "width", "height", "cells", "exits"],
            root.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(1, root.GetProperty("formatVersion").GetInt32());
        Assert.Equal(9100u, root.GetProperty("mapDataId").GetUInt32());
        Assert.Equal(3, root.GetProperty("width").GetInt32());
        Assert.Equal(2, root.GetProperty("height").GetInt32());
        JsonElement.ArrayEnumerator cells = root.GetProperty("cells").EnumerateArray();
        Assert.Equal(6, cells.Count());
        JsonElement first = cells.First();
        JsonElement last = cells.Last();
        Assert.Equal(["surfaceId", "passabilityFlag", "elevation"],
            first.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal((12, 0, -30), (first.GetProperty("surfaceId").GetInt32(),
            first.GetProperty("passabilityFlag").GetInt32(), first.GetProperty("elevation").GetInt32()));
        Assert.Equal((65535, 5, -32768), (last.GetProperty("surfaceId").GetInt32(),
            last.GetProperty("passabilityFlag").GetInt32(), last.GetProperty("elevation").GetInt32()));
        Assert.Equal(JsonValueKind.Array, root.GetProperty("exits").ValueKind);
        Assert.Empty(root.GetProperty("exits").EnumerateArray());
    }

    [Fact]
    public void ExportIsReproducibleAcrossSessionsAndUnaffectedByDisplayName()
    {
        using ProjectDirectory directory = new();
        Guid id;
        string first = directory.PathFor("first.json");
        using (StudioProject project = StudioProject.Create(directory.Project, "World"))
        {
            id = project.CreateTerrain("Ground", 9200, 2, 1, new TerrainCell(0, 1, short.MaxValue));
            project.Save(TestContext.Current.CancellationToken);
            TerrainSourceExporter.Export(project, id, first, TestContext.Current.CancellationToken);
        }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        string second = directory.PathFor("second.json");
        TerrainSourceExporter.Export(reopened, id, second, TestContext.Current.CancellationToken);
        Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
        reopened.EditTerrainCell(id, 0, 0, new TerrainCell(0, 1, short.MaxValue), "Renamed terrain");
        reopened.Save(TestContext.Current.CancellationToken);
        string third = directory.PathFor("third.json");
        TerrainSourceExporter.Export(reopened, id, third, TestContext.Current.CancellationToken);
        Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(third));
    }

    [Fact]
    public void ExportRejectsUnsavedUnknownCanceledAndExistingDestinationsWithoutPublication()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "World");
        Guid id = project.CreateTerrain("Ground", 9300, 1, 1, new TerrainCell(0, 0, 0));
        string destination = directory.PathFor("source.json");
        Assert.Throws<InvalidOperationException>(() =>
            TerrainSourceExporter.Export(project, id, destination, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(destination));
        project.Save(TestContext.Current.CancellationToken);
        Assert.Throws<InvalidDataException>(() =>
            TerrainSourceExporter.Export(project, Guid.NewGuid(), destination, TestContext.Current.CancellationToken));
        using (CancellationTokenSource source = new())
        {
            source.Cancel();
            Assert.ThrowsAny<OperationCanceledException>(() =>
                TerrainSourceExporter.Export(project, id, destination, source.Token));
        }

        Assert.False(File.Exists(destination));
        TerrainSourceExporter.Export(project, id, destination, TestContext.Current.CancellationToken);
        byte[] saved = File.ReadAllBytes(destination);
        Assert.Throws<IOException>(() =>
            TerrainSourceExporter.Export(project, id, destination, TestContext.Current.CancellationToken));
        Assert.Equal(saved, File.ReadAllBytes(destination));
    }
}
