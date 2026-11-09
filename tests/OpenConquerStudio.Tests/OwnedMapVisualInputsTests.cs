using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using OpenConquer.Content.Map;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.ConquerImport;

namespace OpenConquerStudio.Tests;

public sealed class OwnedMapVisualInputsTests
{
    [Fact]
    public void SceneObjectsAndAllReferencedFramesSurviveOfflineSaveReopenAndInventoryExport()
    {
        using ProjectDirectory directory = new();
        TerrainImport input = Input();
        Guid id;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            id = project.ImportTerrain(input, "Ground");
            SourceMapInventory inventory = project.ReadSourceMapInventory(id);
            Assert.Equal(4, inventory.FormatVersion);
            SourceSceneInput scene = Assert.Single(inventory.Scenes);
            Assert.Equal("scenes/terrain.scene", scene.Path);
            Assert.Equal("ani/scenery.ani", Assert.Single(scene.Objects).AnimationPath);
            Assert.Equal("SceneTile", scene.Objects[0].SectionName);
            Assert.Equal(["images/shared.tga", "images/scene.tga"],
                inventory.Frames.Select(frame => frame.Path).ToArray());
            Assert.Equal((1, 1), (project.ReadSourceFrame(id, "images/shared.tga", TestContext.Current.CancellationToken).Width,
                project.ReadSourceFrame(id, "images/shared.tga", TestContext.Current.CancellationToken).Height));
            Assert.Equal((1, 1), (Assert.Single(project.ReadSourceScene(id, "scenes/terrain.scene").Objects).GridWidth,
                project.ReadSourceScene(id, "scenes/terrain.scene").Objects[0].GridHeight));
            project.Save(TestContext.Current.CancellationToken);
            project.PaintTerrainRegion(id, new TerrainRegion(0, 0, 0, 0),
                new TerrainCell(55, 2, -7), "Ground");
            project.Save(TestContext.Current.CancellationToken);
        }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        TerrainAsset terrain = Assert.Single(reopened.Document.Terrains);
        Assert.Equal(id, terrain.Id);
        Assert.Single(terrain.Source!.Scenes);
        Assert.Equal(2, terrain.Source.Frames.Length);
        Assert.Equal(new TerrainCell(55, 2, -7), reopened.ReadTerrainCell(id, 0, 0));
        Assert.Equal(Scene(), File.ReadAllBytes(directory.PathFor("project", "Inputs",
            terrain.Source.Scenes[0].Digest + ".ocscene")));
        Assert.Equal(Tga(21), File.ReadAllBytes(directory.PathFor("project", "Inputs",
            terrain.Source.Frames[0].Digest + ".ocframe")));
        string inventoryPath = directory.PathFor("inventory.json");
        SourceMapInventory.Export(reopened, id, inventoryPath, TestContext.Current.CancellationToken);
        using JsonDocument json = JsonDocument.Parse(File.ReadAllBytes(inventoryPath));
        Assert.Equal(4, json.RootElement.GetProperty("formatVersion").GetInt32());
        Assert.Equal("SceneTile", json.RootElement.GetProperty("scenes")[0].GetProperty("objects")[0]
            .GetProperty("sectionName").GetString());
        Assert.Equal("images/scene.tga",
            json.RootElement.GetProperty("frames")[1].GetProperty("path").GetString());
    }

    [Fact]
    public void CompleteSourceRefreshDoesNotCreateRevisionOrDiscardRedo()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid id = project.ImportTerrain(Input(), "Ground");
        project.Save(TestContext.Current.CancellationToken);
        ProjectDocument saved = project.Document;
        project.Rename("Temporary");
        project.Undo();
        Assert.True(project.CanRedo);
        project.RefreshTerrain(id, Input());
        Assert.Same(saved, project.Document);
        Assert.False(project.IsModified);
        Assert.True(project.CanRedo);

        TerrainImport changed = Input() with
        {
            FrameFiles = [new("images/shared.tga", Tga(99)), new("images/scene.tga", Tga(42))]
        };
        project.RefreshTerrain(id, changed);
        Assert.Equal(id, Assert.Single(project.Document.Terrains).Id);
        Assert.True(project.IsModified);
        Assert.NotEqual(saved.Terrains[0].Source!.Frames[0].Digest,
            project.Document.Terrains[0].Source!.Frames[0].Digest);
        project.Undo();
        Assert.Same(saved, project.Document);
        Assert.True(project.CanRedo);
        project.Redo();
        project.Save(TestContext.Current.CancellationToken);
        Assert.False(project.IsModified);
    }

    [Fact]
    public void MissingOrReorderedSceneFrameInputsNeverMutateAuthoringProject()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        TerrainImport input = Input();
        ProjectDocument before = project.Document;

        Assert.Throws<InvalidDataException>(() => project.ImportTerrain(input with
        {
            SceneFiles = []
        }, "Missing scenery"));
        Assert.Throws<InvalidDataException>(() => project.ImportTerrain(input with
        {
            FrameFiles = default
        }, "Missing frame inventory"));
        Assert.Throws<InvalidDataException>(() => project.ImportTerrain(input with
        {
            SceneFiles = default
        }, "Missing scene inventory"));
        Assert.Throws<InvalidDataException>(() => project.ImportTerrain(input with
        {
            FrameFiles = [new("images/scene.tga", Tga(42)), new("images/shared.tga", Tga(21))]
        }, "Reordered frames"));
        Assert.Throws<InvalidDataException>(() => project.ImportTerrain(input with
        {
            FrameFiles = [new("images/shared.tga", Tga(21))]
        }, "Missing frame"));
        Assert.Throws<InvalidDataException>(() => project.ImportTerrain(input with
        {
            FrameFiles = [new("images/shared.tga", new byte[23]), new("images/scene.tga", Tga(42))]
        }, "Invalid image"));
        Assert.Same(before, project.Document);
        Assert.False(project.IsModified);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CorruptedStoredSceneOrFrameFailsBeforeProjectOpen(bool scene)
    {
        using ProjectDirectory directory = new();
        string digest;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            project.ImportTerrain(Input(), "Ground");
            TerrainSource source = project.Document.Terrains.Single().Source!;
            digest = scene ? source.Scenes[0].Digest : source.Frames[0].Digest;
            project.Save(TestContext.Current.CancellationToken);
        }

        string path = directory.PathFor("project", "Inputs", digest + (scene ? ".ocscene" : ".ocframe"));
        byte[] bytes = File.ReadAllBytes(path);
        bytes[^1] ^= 0xFF;
        File.WriteAllBytes(path, bytes);
        Assert.Throws<InvalidDataException>(() => StudioProject.Open(directory.Project,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SceneWithoutAnimationObjectsRemainsAValidOwnedInput()
    {
        using ProjectDirectory directory = new();
        TerrainImport original = Input();
        byte[] emptyScene = new byte[4];
        TerrainImport source = original with
        {
            SceneFiles = [new("scenes/terrain.scene", emptyScene)],
            AnimationFiles = [new("ani/puzzle.ani", PuzzleIndex())],
            FrameFiles = [new("images/shared.tga", Tga(21))]
        };
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid id = project.ImportTerrain(source, "Ground");
        Assert.Empty(project.ReadSourceScene(id, "scenes/terrain.scene").Objects);
        project.Save(TestContext.Current.CancellationToken);
        Assert.Single(project.Document.Terrains);
    }

    private static TerrainImport Input()
    {
        byte[] map = Map();
        GameMapCellGrid grid = GameMapCellGrid.Parse(map);
        GameMapSceneSections source = GameMapSceneSections.Parse(map, grid.BytesConsumed, grid.HeaderField0);
        TerrainImport extracted = ClientTerrainImporter.Read(9100, "maps/ground.dmap", 42, grid, source,
            TestContext.Current.CancellationToken);
        return extracted with
        {
            DecodedSource = map,
            PuzzleFiles =
            [
                new("puzzles/ground.pul", OwnedMapPuzzleWorkflowTests.Puzzle(2, 2, 2, 9)),
                new("puzzles/layer.pul", OwnedMapPuzzleWorkflowTests.Puzzle(3, 1, 12, 8))
            ],
            SceneFiles = [new("scenes/terrain.scene", Scene())],
            AnimationFiles =
            [
                new("ani/puzzle.ani", PuzzleIndex()),
                new("ani/scenery.ani", Encoding.Latin1.GetBytes(
                    "[SceneTile]\nFrameAmount=1\nFrame0=images/scene.tga\n"))
            ],
            FrameFiles = [new("images/shared.tga", Tga(21)), new("images/scene.tga", Tga(42))]
        };
    }

    private static byte[] PuzzleIndex() => Encoding.Latin1.GetBytes(
        "[Puzzle2]\nFrameAmount=1\nFrame0=images/shared.tga\n" +
        "[Puzzle9]\nFrameAmount=1\nFrame0=images/shared.tga\n" +
        "[Puzzle12]\nFrameAmount=1\nFrame0=images/shared.tga\n" +
        "[Puzzle8]\nFrameAmount=1\nFrame0=images/shared.tga\n");

    private static byte[] Tga(byte red)
    {
        byte[] bytes = new byte[23];
        bytes[2] = 10;
        bytes[12] = 1;
        bytes[14] = 1;
        bytes[16] = 32;
        bytes[17] = 8;
        bytes[18] = 0x80;
        bytes[21] = red;
        bytes[22] = 0xFF;
        return bytes;
    }

    private static byte[] Scene()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.Latin1, leaveOpen: true);
        writer.Write(1);
        byte[] path = new byte[0x100];
        Encoding.Latin1.GetBytes("ani/scenery.ani").CopyTo(path, 0);
        writer.Write(path);
        byte[] name = new byte[0x40];
        Encoding.Latin1.GetBytes("SceneTile").CopyTo(name, 0);
        writer.Write(name);
        writer.Write(8);
        writer.Write(9);
        writer.Write(100);
        writer.Write(1);
        writer.Write(1);
        writer.Write(unchecked((int)0xFF010203));
        writer.Write(3);
        writer.Write(4);
        writer.Write(5);
        writer.Write(2);
        writer.Write(15);
        writer.Write(-7);
        return stream.ToArray();
    }

    private static byte[] Map()
    {
        byte[] map = ClientTerrainImporterTests.MapSource();
        GameMapCellGrid grid = GameMapCellGrid.Parse(map);
        int sceneryType = grid.BytesConsumed + sizeof(int) + 3 * sizeof(int) + sizeof(int);
        const int OldLength = 0x48;
        const int NewLength = 0x10C;
        byte[] expanded = new byte[map.Length + NewLength - OldLength];
        map.AsSpan(0, sceneryType).CopyTo(expanded);
        BinaryPrimitives.WriteInt32LittleEndian(expanded.AsSpan(sceneryType), 1);
        byte[] scenePayload = new byte[NewLength];
        Encoding.Latin1.GetBytes("scenes/terrain.scene").CopyTo(scenePayload, 0);
        BinaryPrimitives.WriteInt32LittleEndian(scenePayload.AsSpan(0x104), 17);
        BinaryPrimitives.WriteInt32LittleEndian(scenePayload.AsSpan(0x108), 29);
        scenePayload.CopyTo(expanded, sceneryType + sizeof(int));
        map.AsSpan(sceneryType + sizeof(int) + OldLength).CopyTo(expanded.AsSpan(sceneryType + sizeof(int) + NewLength));
        return expanded;
    }
}
