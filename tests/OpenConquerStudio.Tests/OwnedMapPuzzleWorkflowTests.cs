using System.Buffers.Binary;
using System.Text;
using OpenConquer.Content.Map;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.ConquerImport;

namespace OpenConquerStudio.Tests;

public sealed class OwnedMapPuzzleWorkflowTests
{
    [Fact]
    public void CapturedPuzzleGridsRoundTripOfflineAndSurviveTerrainEdits()
    {
        using ProjectDirectory directory = new();
        byte[] map = ClientTerrainImporterTests.MapSource();
        byte[] ground = Puzzle(2, 2, 2, 9);
        byte[] overlay = Puzzle(3, 1, 12, 8);
        TerrainImport source = Input(map) with
        {
            PuzzleFiles = [new("puzzles/ground.pul", ground), new("puzzles/layer.pul", overlay)]
        };
        Guid id;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            id = project.ImportTerrain(source, "Ground");
            TerrainAsset terrain = Assert.Single(project.Document.Terrains);
            Assert.Equal(2, terrain.Source!.Puzzles.Length);
            SourceMapInventory inventory = project.ReadSourceMapInventory(id);
            Assert.Equal(2, inventory.FormatVersion);
            Assert.Equal(["puzzles/ground.pul", "puzzles/layer.pul"],
                inventory.Puzzles.Select(p => p.Path).ToArray());
            Assert.All(inventory.Puzzles, item => Assert.NotNull(item.Digest));
            Assert.Equal(4, project.ReadSourcePuzzle(id, "puzzles/ground.pul").Tiles.Length);
            Assert.Equal((3, 1), (project.ReadSourcePuzzle(id, "puzzles/layer.pul").Columns,
                project.ReadSourcePuzzle(id, "puzzles/layer.pul").Rows));
            project.Save(TestContext.Current.CancellationToken);
            project.PaintTerrainRegion(id, new TerrainRegion(0, 0, 0, 0),
                new TerrainCell(99, 0, 5), "Changed");
            project.Undo();
            Assert.Equal("Ground", project.Document.Terrains.Single().Name);
            project.Redo();
            project.Save(TestContext.Current.CancellationToken);
        }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        TerrainAsset restored = Assert.Single(reopened.Document.Terrains);
        Assert.Equal("Changed", restored.Name);
        Assert.Equal(2, reopened.ReadSourceMapInventory(id).Puzzles.Length);
        Assert.Equal(overlay, File.ReadAllBytes(directory.PathFor("project", "Inputs",
            restored.Source!.Puzzles[1].Digest! + ".ocpuzzle")));
        Assert.Equal("ani/puzzle.ani", reopened.ReadSourcePuzzle(id, "puzzles/layer.pul").TextureIndexPath);
    }

    [Fact]
    public void ExplicitlyAbsentGroundIsReportedButAbsentOverlayIsRejected()
    {
        using ProjectDirectory directory = new();
        byte[] map = ClientTerrainImporterTests.MapSource();
        byte[] overlay = Puzzle(1, 1, 5, 6);
        TerrainImport input = Input(map) with
        {
            PuzzleFiles = [new("puzzles/ground.pul", null), new("puzzles/layer.pul", overlay)]
        };
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid id = project.ImportTerrain(input, "Ground");
        SourceMapInventory inventory = project.ReadSourceMapInventory(id);
        Assert.Null(inventory.Puzzles[0].Digest);
        Assert.Equal(0, inventory.Puzzles[0].ByteLength);
        Assert.NotNull(inventory.Puzzles[1].Digest);
        Assert.Throws<InvalidDataException>(() => project.ReadSourcePuzzle(id, "puzzles/ground.pul"));
        project.Save(TestContext.Current.CancellationToken);

        ProjectDocument saved = project.Document;
        Assert.Throws<InvalidDataException>(() => project.RefreshTerrain(id, input with
        {
            PuzzleFiles = [new("puzzles/ground.pul", overlay), new("puzzles/layer.pul", null)]
        }));
        Assert.Same(saved, project.Document);
        Assert.False(project.IsModified);
    }

    [Fact]
    public void RejectsIncompleteReorderedAndMalformedPuzzleInputsWithoutMutation()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        byte[] map = ClientTerrainImporterTests.MapSource();
        byte[] puzzle = Puzzle(2, 2, 0, 3);
        TerrainImport input = Input(map);
        ProjectDocument original = project.Document;

        Assert.Throws<InvalidDataException>(() => project.ImportTerrain(input with
        {
            PuzzleFiles = [new("puzzles/ground.pul", puzzle)]
        }, "Incomplete"));
        Assert.Throws<InvalidDataException>(() => project.ImportTerrain(input with
        {
            PuzzleFiles = [new("puzzles/layer.pul", puzzle), new("puzzles/ground.pul", puzzle)]
        }, "Reordered"));
        Assert.Throws<InvalidDataException>(() => project.ImportTerrain(input with
        {
            PuzzleFiles = [new("puzzles/ground.pul", puzzle), new("puzzles/layer.pul", new byte[300])]
        }, "Malformed"));
        Assert.Same(original, project.Document);
        Assert.False(project.IsModified);
    }

    [Fact]
    public void AlteredCapturedPuzzlePreventsProjectOpen()
    {
        using ProjectDirectory directory = new();
        string digest;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            byte[] map = ClientTerrainImporterTests.MapSource();
            byte[] puzzle = Puzzle(2, 2, 3, 4);
            project.ImportTerrain(Input(map) with
            {
                PuzzleFiles = [new("puzzles/ground.pul", puzzle), new("puzzles/layer.pul", puzzle)]
            }, "Ground");
            digest = project.Document.Terrains.Single().Source!.Puzzles[0].Digest!;
            project.Save(TestContext.Current.CancellationToken);
        }
        string path = directory.PathFor("project", "Inputs", digest + ".ocpuzzle");
        byte[] corrupted = File.ReadAllBytes(path);
        corrupted[^1] ^= 1;
        File.WriteAllBytes(path, corrupted);
        Assert.Throws<InvalidDataException>(() => StudioProject.Open(directory.Project,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void RefreshPreservesTerrainIdentityAndUpdatesPuzzleCaptureAtomically()
    {
        using ProjectDirectory directory = new();
        byte[] map = ClientTerrainImporterTests.MapSource();
        byte[] original = Puzzle(2, 2, 3, 4);
        byte[] changed = Puzzle(2, 2, 6, 7);
        TerrainImport first = Input(map) with
        {
            PuzzleFiles = [new("puzzles/ground.pul", original), new("puzzles/layer.pul", original)]
        };
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid id = project.ImportTerrain(first, "Ground");
        project.Save(TestContext.Current.CancellationToken);
        string digest = project.Document.Terrains.Single().Source!.Puzzles[0].Digest!;
        project.RefreshTerrain(id, first with
        {
            PuzzleFiles = [new("puzzles/ground.pul", changed), new("puzzles/layer.pul", original)]
        });
        Assert.Equal(id, project.Document.Terrains.Single().Id);
        Assert.NotEqual(digest, project.Document.Terrains.Single().Source!.Puzzles[0].Digest);
        Assert.True(project.IsModified);
        project.Undo();
        Assert.Equal(digest, project.Document.Terrains.Single().Source!.Puzzles[0].Digest);
        Assert.True(project.CanRedo);
        project.Redo();
        project.Save(TestContext.Current.CancellationToken);
        Assert.Equal(changed, File.ReadAllBytes(directory.PathFor("project", "Inputs",
            project.Document.Terrains.Single().Source!.Puzzles[0].Digest! + ".ocpuzzle")));
    }

    private static TerrainImport Input(byte[] source)
    {
        GameMapCellGrid grid = GameMapCellGrid.Parse(source);
        GameMapSceneSections sections = GameMapSceneSections.Parse(source, grid.BytesConsumed, grid.HeaderField0);
        return ClientTerrainImporter.Read(9100, "maps/ground.dmap", 42, grid, sections,
            TestContext.Current.CancellationToken) with
        {
            DecodedSource = source
        };
    }

    private static byte[] Puzzle(int width, int height, ushort first, ushort second)
    {
        byte[] bytes = new byte[272 + width * height * sizeof(ushort)];
        "PUZZLE\0\0"u8.CopyTo(bytes);
        Encoding.Latin1.GetBytes("ani/puzzle.ani").CopyTo(bytes, 8);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(264), width);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(268), height);
        for (int i = 0; i < width * height; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(272 + i * 2), i % 2 == 0 ? first : second);
        }
        return bytes;
    }
}
