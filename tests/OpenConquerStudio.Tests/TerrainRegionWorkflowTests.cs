using System.Text.Json;
using OpenConquerStudio.Authoring;

namespace OpenConquerStudio.Tests;

public sealed class TerrainRegionWorkflowTests
{
    [Fact]
    public void RectanglePaintIsOneUndoableRevisionAndRoundTripsIntoServerSource()
    {
        using ProjectDirectory directory = new();
        Guid id;
        TerrainCell original = new(8, 2, -3);
        TerrainCell brush = new(ushort.MaxValue, 7, short.MinValue);
        TerrainRegion region = new(1, 0, 3, 1);
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            id = project.CreateTerrain("Ground", 8100, 4, 3, original);
            Guid marker = Guid.NewGuid();
            project.EditTerrainExits(id, [new TerrainExitMarker(marker, -2, 40, 9)]);
            project.Save(TestContext.Current.CancellationToken);
            ProjectDocument before = project.Document;

            project.PaintTerrainRegion(id, region, brush, "Painted ground");
            Assert.Equal("Painted ground", project.Document.Terrains.Single().Name);
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    Assert.Equal(x >= 1 && y <= 1 ? brush : original, project.ReadTerrainCell(id, x, y));
                }
            }

            project.Undo();
            Assert.Same(before, project.Document);
            Assert.False(project.IsModified);
            Assert.Equal(original, project.ReadTerrainCell(id, 3, 1));
            Assert.True(project.CanRedo);

            project.Redo();
            Assert.Equal(brush, project.ReadTerrainCell(id, 3, 1));
            project.Save(TestContext.Current.CancellationToken);
            Assert.Equal(marker, Assert.Single(project.Document.Terrains.Single().Exits).Id);
        }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        Assert.Equal("Painted ground", reopened.Document.Terrains.Single().Name);
        Assert.Equal(brush, reopened.ReadTerrainCell(id, 1, 0));
        Assert.Equal(original, reopened.ReadTerrainCell(id, 0, 0));
        Assert.Equal(original, reopened.ReadTerrainCell(id, 3, 2));

        string path = directory.PathFor("painted-terrain.json");
        TerrainSourceExporter.Export(reopened, id, path, TestContext.Current.CancellationToken);
        using JsonDocument json = JsonDocument.Parse(File.ReadAllBytes(path));
        JsonElement[] cells = json.RootElement.GetProperty("cells").EnumerateArray().ToArray();
        Assert.Equal(12, cells.Length);
        Assert.Equal(65535, cells[1].GetProperty("surfaceId").GetInt32());
        Assert.Equal(-32768, cells[7].GetProperty("elevation").GetInt32());
        Assert.Equal(8, cells[8].GetProperty("surfaceId").GetInt32());
        JsonElement exit = Assert.Single(json.RootElement.GetProperty("exits").EnumerateArray());
        Assert.Equal(-2, exit.GetProperty("x").GetInt32());
        Assert.Equal(9, exit.GetProperty("passwayIndex").GetInt32());
    }

    [Fact]
    public void InvalidAndIdenticalRegionsNeverReplaceRevisionOrDiscardRedo()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        TerrainCell cell = new(1, 0, 0);
        Guid id = project.CreateTerrain("Ground", 8200, 3, 2, cell);
        project.Save(TestContext.Current.CancellationToken);
        ProjectDocument saved = project.Document;
        byte[] index = File.ReadAllBytes(directory.Manifest);

        project.Rename("Temporary");
        project.Undo();
        Assert.True(project.CanRedo);
        foreach (TerrainRegion invalid in new[]
        {
            new TerrainRegion(-1, 0, 1, 1),
            new TerrainRegion(0, 0, 3, 1),
            new TerrainRegion(0, 0, 1, 2),
            new TerrainRegion(2, 1, 1, 1)
        })
        {
            Assert.Throws<InvalidDataException>(() => project.PaintTerrainRegion(id, invalid, cell, "Ground"));
            Assert.Same(saved, project.Document);
        }

        Assert.Throws<InvalidDataException>(() => project.PaintTerrainRegion(id, new TerrainRegion(0, 0, 2, 1), cell, ""));
        project.PaintTerrainRegion(id, new TerrainRegion(0, 0, 2, 1), cell, "Ground");
        Assert.Same(saved, project.Document);
        Assert.False(project.IsModified);
        Assert.True(project.CanRedo);
        project.Save(TestContext.Current.CancellationToken);
        Assert.Equal(index, File.ReadAllBytes(directory.Manifest));
        project.Redo();
        Assert.Equal("Temporary", project.Document.Name);
    }

    [Fact]
    public void MaximumSizedRegionPaintRemainsBoundedAndReversible()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        TerrainCell before = new(0, 0, 0);
        TerrainCell after = new(45, 65535, 1234);
        Guid id = project.CreateTerrain("Large", 8300, 512, 512, before);
        project.Save(TestContext.Current.CancellationToken);
        project.PaintTerrainRegion(id, new TerrainRegion(0, 0, 511, 511), after, "Large");
        Assert.Equal(after, project.ReadTerrainCell(id, 0, 0));
        Assert.Equal(after, project.ReadTerrainCell(id, 511, 511));
        project.Undo();
        Assert.False(project.IsModified);
        Assert.Equal(before, project.ReadTerrainCell(id, 511, 511));
        project.Redo();
        Assert.Equal(after, project.ReadTerrainCell(id, 256, 256));
    }
}
