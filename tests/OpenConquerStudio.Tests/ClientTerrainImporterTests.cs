using System.Buffers.Binary;
using System.Text;
using OpenConquer.Content.Map;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.ConquerImport;

namespace OpenConquerStudio.Tests;

public sealed class ClientTerrainImporterTests
{
    [Fact]
    public void ExtractsAllRawCellsAndOrderedExitsWhileReportingUnrepresentedComponents()
    {
        byte[] source = MapSource();
        GameMapCellGrid grid = GameMapCellGrid.Parse(source);
        GameMapSceneSections scene = GameMapSceneSections.Parse(source, grid.BytesConsumed, grid.HeaderField0);
        TerrainImport extracted = ClientTerrainImporter.Read(9100, "maps/ground.dmap", 42, grid, scene,
            TestContext.Current.CancellationToken);

        Assert.Equal(9100u, extracted.MapDataId);
        Assert.Equal((2, 2), (extracted.Width, extracted.Height));
        Assert.Equal([new TerrainCell(12, 1, -3), new TerrainCell(25, 2, 10),
            new TerrainCell(35, 3, -5), new TerrainCell(ushort.MaxValue, 4, short.MaxValue)], extracted.Cells.ToArray());
        TerrainExitMarker marker = Assert.Single(extracted.Exits);
        Assert.Equal((-2, 400, uint.MaxValue), (marker.X, marker.Y, marker.PasswayIndex));
        Assert.NotEqual(Guid.Empty, marker.Id);
        Assert.Equal(new TerrainSource("maps/ground.dmap", "puzzles/ground.pul", 42,
            1000, 77, 1, 1), extracted.Source);

        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid id = project.ImportTerrain(extracted, "Source ground");
        project.Save(TestContext.Current.CancellationToken);
        Assert.Equal(new TerrainCell(ushort.MaxValue, 4, short.MaxValue),
            project.ReadTerrainCell(id, 1, 1));
        Assert.Equal(uint.MaxValue, project.Document.Terrains.Single().Exits.Single().PasswayIndex);
        Assert.Equal(1, project.Document.Terrains.Single().Source!.OverlayGroupCount);
    }

    [Fact]
    public void ExtractedSourceCanRefreshIdenticallyWithoutRewritingProject()
    {
        byte[] source = MapSource();
        GameMapCellGrid grid = GameMapCellGrid.Parse(source);
        GameMapSceneSections scene = GameMapSceneSections.Parse(source, grid.BytesConsumed, grid.HeaderField0);
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid id = project.ImportTerrain(
            ClientTerrainImporter.Read(9100, "maps/ground.dmap", 42, grid, scene), "Source ground");
        project.Save(TestContext.Current.CancellationToken);
        ProjectDocument saved = project.Document;

        project.RefreshTerrain(id,
            ClientTerrainImporter.Read(9100, "maps/ground.dmap", 42, grid, scene));
        Assert.Same(saved, project.Document);
        Assert.False(project.IsModified);
    }

    private static byte[] MapSource()
    {
        using MemoryStream stream = new();
        byte[] header = new byte[276];
        BinaryPrimitives.WriteUInt32LittleEndian(header, 1000);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), 77);
        Encoding.Latin1.GetBytes("puzzles/ground.pul").CopyTo(header, 8);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(268), 2);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(272), 2);
        stream.Write(header);
        using BinaryWriter writer = new(stream, Encoding.Latin1, leaveOpen: true);
        TerrainCell[] cells =
        [
            new(12, 1, -3), new(25, 2, 10),
            new(35, 3, -5), new(ushort.MaxValue, 4, short.MaxValue)
        ];
        for (int y = 0; y < 2; y++)
        {
            uint checksum = 0;
            for (int x = 0; x < 2; x++)
            {
                TerrainCell cell = cells[y * 2 + x];
                writer.Write(cell.PassabilityFlag);
                writer.Write(cell.SurfaceId);
                writer.Write(cell.Elevation);
                checksum = unchecked(checksum
                    + ((uint)cell.SurfaceId + (uint)y + 1) * cell.PassabilityFlag
                    + ((uint)cell.SurfaceId + (uint)x + 1) * (uint)(cell.Elevation + 2));
            }
            writer.Write(checksum);
        }

        writer.Write(1);
        writer.Write(-2);
        writer.Write(400);
        writer.Write(-1);
        writer.Write(1);
        writer.Write(10);
        writer.Write(new byte[0x48]);
        writer.Write(1);
        writer.Write(0);
        writer.Write(4);
        writer.Write(31);
        writer.Write(37);
        writer.Write(1);
        writer.Write(8);
        byte[] puzzle = new byte[0x104];
        Encoding.Latin1.GetBytes("puzzles/layer.pul").CopyTo(puzzle, 0);
        writer.Write(puzzle);
        return stream.ToArray();
    }
}
