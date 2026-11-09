using OpenConquer.Content.Map;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.ConquerImport;

namespace OpenConquerStudio.Tests;

public sealed class SourceTerrainContentTests
{
    [Fact]
    public void LocalMap1010TerrainExtractMatchesEveryCellAndExitThroughSaveReopen()
    {
        string repository = ProjectDirectory.RepositoryRoot;
        string content = Environment.GetEnvironmentVariable("OPENCONQUER_CLIENT_CONTENT_ROOT")
            ?? Path.Combine(repository, "content", "retail-5517", "payload");
        Assert.SkipUnless(File.Exists(Path.Combine(content, "ini", "GameMap.dat")),
            "Local map source content is unavailable. Set OPENCONQUER_CLIENT_ROOT and OPENCONQUER_CLIENT_CONTENT_ROOT.");

        using ProjectDirectory directory = new();
        Guid terrainId;
        GameMapContent map;
        using (StudioProject project = StudioProject.Create(directory.Project, "Source terrain"))
        {
            ClientProjectConnection connection = ClientProjectConnection.Attach(project, repository, content,
                TestContext.Current.CancellationToken);
            map = connection.ReadMap(1010);
            long cells = (long)map.CellGrid.Width * map.CellGrid.Height;
            if (cells > ProjectDocument.MaximumTotalTerrainCells ||
                map.Scene.ExitMarkers.Count > ProjectDocument.MaximumTerrainExitMarkers ||
                map.CellGrid.Width > 1024 || map.CellGrid.Height > 1024)
            {
                Assert.Throws<InvalidDataException>(() => ClientTerrainImporter.Read(map,
                    TestContext.Current.CancellationToken));
                Assert.Empty(project.Document.Terrains);
                return;
            }

            TerrainImport extracted = ClientTerrainImporter.Read(map, TestContext.Current.CancellationToken);
            terrainId = project.ImportTerrain(extracted, "Imported ground");
            project.Save(TestContext.Current.CancellationToken);
        }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        TerrainAsset terrain = Assert.Single(reopened.Document.Terrains);
        Assert.Equal(map.MapDataId, terrain.MapDataId);
        Assert.Equal(map.ArchivePath, terrain.Source!.MapPath);
        Assert.Equal(map.CellGrid.PuzzlePath, terrain.Source.PuzzlePath);
        for (int y = 0; y < map.CellGrid.Height; y++)
        {
            for (int x = 0; x < map.CellGrid.Width; x++)
            {
                GameMapCell expected = map.CellGrid.GetCell(x, y);
                Assert.Equal(new TerrainCell(expected.SurfaceId, expected.PassabilityFlag, expected.Elevation),
                    reopened.ReadTerrainCell(terrainId, x, y));
            }
        }

        Assert.Equal(map.Scene.ExitMarkers.Count, terrain.Exits.Length);
        for (int i = 0; i < terrain.Exits.Length; i++)
        {
            GameMapExitMarker marker = map.Scene.ExitMarkers[i];
            Assert.Equal((marker.Field0, marker.Field1, unchecked((uint)marker.Field2)),
                (terrain.Exits[i].X, terrain.Exits[i].Y, terrain.Exits[i].PasswayIndex));
        }
    }
}
