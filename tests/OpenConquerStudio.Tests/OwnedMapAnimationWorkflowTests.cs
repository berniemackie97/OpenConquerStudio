using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using OpenConquer.Content.Map;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.ConquerImport;

namespace OpenConquerStudio.Tests;

public sealed class OwnedMapAnimationWorkflowTests
{
    [Fact]
    public void PuzzleTileAnimationSectionsAreCapturedInExactOrderAndReopenOffline()
    {
        using ProjectDirectory directory = new();
        TerrainImport captured = Input(ClientTerrainImporterTests.MapSource());
        Guid id;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            id = project.ImportTerrain(captured, "Ground");
            SourceMapInventory inventory = project.ReadSourceMapInventory(id);
            Assert.Equal(3, inventory.FormatVersion);
            SourceAnimationIndexInput index = Assert.Single(inventory.AnimationIndexes);
            Assert.Equal("ani/puzzle.ani", index.Path);
            Assert.Equal(["Puzzle2", "Puzzle9", "Puzzle12", "Puzzle8"],
                index.Sections.Select(section => section.Name).ToArray());
            Assert.Equal(["textures/2a.dds", "textures/2b.dds"],
                index.Sections[0].FramePaths.ToArray());
            Assert.Equal(["textures/8.dds"], index.Sections[3].FramePaths.ToArray());
            Assert.Equal(index.Sections.ToArray(), project.ReadSourceAnimationIndex(id, index.Path).ToArray());
            project.Save(TestContext.Current.CancellationToken);
        }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        TerrainAsset terrain = Assert.Single(reopened.Document.Terrains);
        Assert.Equal(id, terrain.Id);
        Assert.Equal(1, terrain.Source!.AnimationIndexes.Length);
        string digest = terrain.Source.AnimationIndexes[0].Digest;
        Assert.Equal(Indexes(), File.ReadAllBytes(directory.PathFor("project", "Inputs", digest + ".ocani")));
        Assert.Equal(["Puzzle2", "Puzzle9", "Puzzle12", "Puzzle8"],
            reopened.ReadSourceAnimationIndex(id, "ani/puzzle.ani").Select(section => section.Name).ToArray());

        string output = directory.PathFor("map-inventory.json");
        SourceMapInventory.Export(reopened, id, output, TestContext.Current.CancellationToken);
        using JsonDocument json = JsonDocument.Parse(File.ReadAllBytes(output));
        Assert.Equal(3, json.RootElement.GetProperty("formatVersion").GetInt32());
        Assert.Equal("textures/2b.dds",
            json.RootElement.GetProperty("animationIndexes")[0].GetProperty("sections")[0]
                .GetProperty("framePaths")[1].GetString());
    }

    [Fact]
    public void ExactNoOpRefreshPreservesRevisionAndRedoWhileChangedIndexesAreUndoable()
    {
        using ProjectDirectory directory = new();
        byte[] map = ClientTerrainImporterTests.MapSource();
        TerrainImport initial = Input(map);
        Guid id;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            id = project.ImportTerrain(initial, "Ground");
            project.Save(TestContext.Current.CancellationToken);
            ProjectDocument saved = project.Document;
            project.Rename("Temporary");
            project.Undo();
            Assert.True(project.CanRedo);
    
            project.RefreshTerrain(id, Input(map));
            Assert.Same(saved, project.Document);
            Assert.False(project.IsModified);
            Assert.True(project.CanRedo);
    
            byte[] changed = Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(Indexes())
                .Replace("textures/2b.dds", "textures/new.dds", StringComparison.Ordinal));
            TerrainImport next = Input(map) with
            {
                AnimationFiles = [new("ani/puzzle.ani", changed)]
            };
            project.RefreshTerrain(id, next);
            Assert.Equal(id, Assert.Single(project.Document.Terrains).Id);
            Assert.True(project.IsModified);
            Assert.Equal(["textures/2a.dds", "textures/new.dds"],
                project.ReadSourceAnimationIndex(id, "ani/puzzle.ani")[0].FramePaths.ToArray());
            project.Undo();
            Assert.Same(saved, project.Document);
            Assert.Equal(["textures/2a.dds", "textures/2b.dds"],
                project.ReadSourceAnimationIndex(id, "ani/puzzle.ani")[0].FramePaths.ToArray());
            project.Redo();
            project.Save(TestContext.Current.CancellationToken);
            Assert.False(project.IsModified);
        }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        Assert.Equal("textures/new.dds", reopened.ReadSourceAnimationIndex(id, "ani/puzzle.ani")[0].FramePaths[1]);
    }

    [Fact]
    public void MissingOrIncorrectReferencedSectionRejectsImportWithoutMutation()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        TerrainImport input = Input(ClientTerrainImporterTests.MapSource());
        ProjectDocument saved = project.Document;

        Assert.Throws<InvalidDataException>(() => project.ImportTerrain(input with
        {
            AnimationFiles = []
        }, "No index"));
        Assert.Throws<InvalidDataException>(() => project.ImportTerrain(input with
        {
            AnimationFiles = [new("ani/other.ani", Indexes())]
        }, "Wrong order"));
        Assert.Throws<InvalidDataException>(() => project.ImportTerrain(input with
        {
            AnimationFiles = [new("ani/puzzle.ani", Encoding.Latin1.GetBytes(
                "[Puzzle2]\nFrameAmount=1\nFrame0=textures/2a.dds\n"))]
        }, "Missing section"));
        Assert.Throws<InvalidDataException>(() => project.ImportTerrain(input with
        {
            AnimationFiles = [new("ani/puzzle.ani", Encoding.Latin1.GetBytes(
                "[Puzzle2]\nFrameAmount=1\nFrame0=../outside.dds\n" +
                "[Puzzle9]\nFrameAmount=1\nFrame0=textures/9.dds\n" +
                "[Puzzle12]\nFrameAmount=1\nFrame0=textures/12.dds\n" +
                "[Puzzle8]\nFrameAmount=1\nFrame0=textures/8.dds\n"))]
        }, "Unsafe path"));
        Assert.Same(saved, project.Document);
        Assert.False(project.IsModified);
    }

    [Fact]
    public void CapturedIndexIntegrityFailureRejectsProjectBeforeOpening()
    {
        using ProjectDirectory directory = new();
        string digest;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            project.ImportTerrain(Input(ClientTerrainImporterTests.MapSource()), "Ground");
            digest = project.Document.Terrains.Single().Source!.AnimationIndexes[0].Digest;
            project.Save(TestContext.Current.CancellationToken);
        }
        string file = directory.PathFor("project", "Inputs", digest + ".ocani");
        byte[] bytes = File.ReadAllBytes(file);
        bytes[bytes.Length / 2] ^= 1;
        File.WriteAllBytes(file, bytes);
        Assert.Throws<InvalidDataException>(() => StudioProject.Open(directory.Project,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void AnimatedSceneRecordsAreIncludedInExactIndexAndSectionRequirements()
    {
        byte[] source = AnimatedOverlayMap();
        TerrainImport extracted = Input(source, includeSceneIndex: true);
        using ProjectDirectory directory = new();
        Guid id;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            id = project.ImportTerrain(extracted, "Ground");
            SourceMapInventory inventory = project.ReadSourceMapInventory(id);
            Assert.Equal(["ani/puzzle.ani", "ani/effect.ani"],
                inventory.AnimationIndexes.Select(x => x.Path).ToArray());
            Assert.Equal("Spark", inventory.AnimationIndexes[1].Sections[0].Name);
            Assert.Equal("textures/spark.dds", inventory.AnimationIndexes[1].Sections[0].FramePaths[0]);
            project.Save(TestContext.Current.CancellationToken);
        }

        using StudioProject opened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        Assert.Equal("Spark", opened.ReadSourceAnimationIndex(id, "ani/effect.ani")[0].Name);
    }

    private static TerrainImport Input(byte[] source, bool includeSceneIndex = false)
    {
        GameMapCellGrid grid = GameMapCellGrid.Parse(source);
        GameMapSceneSections scene = GameMapSceneSections.Parse(source, grid.BytesConsumed, grid.HeaderField0);
        TerrainImport imported = ClientTerrainImporter.Read(9100, "maps/ground.dmap", 42, grid, scene,
            TestContext.Current.CancellationToken);
        byte[] ground = OwnedMapPuzzleWorkflowTests.Puzzle(2, 2, 2, 9);
        byte[] overlay = OwnedMapPuzzleWorkflowTests.Puzzle(3, 1, 12, 8);
        return imported with
        {
            DecodedSource = source,
            PuzzleFiles = [new("puzzles/ground.pul", ground), new("puzzles/layer.pul", overlay)],
            AnimationFiles = includeSceneIndex
                ? [new("ani/puzzle.ani", Indexes()),
                    new("ani/effect.ani", Encoding.Latin1.GetBytes(
                        "[Spark]\nFrameAmount=1\nFrame0=textures/spark.dds\n"))]
                : [new("ani/puzzle.ani", Indexes())]
        };
    }

    private static byte[] Indexes() => Encoding.Latin1.GetBytes(
        "[Puzzle2]\nFrameAmount=2\nFrame0=textures/2a.dds\nFrame1=textures/2b.dds\n" +
        "[Puzzle9]\nFrameAmount=1\nFrame0=textures/9.dds\n" +
        "[Puzzle12]\nFrameAmount=1\nFrame0=textures/12.dds\n" +
        "[Puzzle8]\nFrameAmount=1\nFrame0=textures/8.dds\n");

    private static byte[] AnimatedOverlayMap()
    {
        byte[] original = ClientTerrainImporterTests.MapSource();
        int childCountOffset = original.Length - 0x104 - 8;
        byte[] enlarged = new byte[original.Length + 4 + 0x19C];
        original.CopyTo(enlarged, 0);
        BinaryPrimitives.WriteInt32LittleEndian(enlarged.AsSpan(childCountOffset), 2);
        int start = original.Length;
        BinaryPrimitives.WriteInt32LittleEndian(enlarged.AsSpan(start), 3);
        Encoding.Latin1.GetBytes("ani/effect.ani").CopyTo(enlarged, start + 4);
        Encoding.Latin1.GetBytes("Spark").CopyTo(enlarged, start + 4 + 0x104);
        return enlarged;
    }
}
