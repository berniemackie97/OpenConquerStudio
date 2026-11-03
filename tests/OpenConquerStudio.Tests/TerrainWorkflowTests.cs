using System.Buffers.Binary;
using OpenConquerStudio.Authoring;

namespace OpenConquerStudio.Tests;

public sealed class TerrainWorkflowTests
{
    [Fact]
    public void CreateEditUndoRedoSaveReopenPreservesCellsAndIdentity()
    {
        using ProjectDirectory directory = new();
        Guid id;
        byte[] originalIndex;
        TerrainCell initial = new(25, 1, -30);
        TerrainCell updated = new(ushort.MaxValue, 5, short.MinValue);
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            id = project.CreateTerrain("Forest", 2000, 3, 2, initial);
            project.Save(TestContext.Current.CancellationToken);
            originalIndex = File.ReadAllBytes(directory.Manifest);
            Assert.Equal(initial, project.ReadTerrainCell(id, 0, 0));
            Assert.Equal(initial, project.ReadTerrainCell(id, 2, 1));

            project.EditTerrainCell(id, 2, 1, updated, "Forest entrance");
            Assert.True(project.IsModified);
            Assert.Equal(initial, project.ReadTerrainCell(id, 2, 0));
            Assert.Equal(updated, project.ReadTerrainCell(id, 2, 1));
            Assert.Equal("Forest entrance", Assert.Single(project.Document.Terrains).Name);
            Assert.Equal(id, Assert.Single(project.Document.Terrains).Id);

            project.Undo();
            Assert.False(project.IsModified);
            Assert.Equal(initial, project.ReadTerrainCell(id, 2, 1));
            Assert.Equal("Forest", Assert.Single(project.Document.Terrains).Name);
            project.Redo();
            project.Save(TestContext.Current.CancellationToken);
            Assert.NotEqual(originalIndex, File.ReadAllBytes(directory.Manifest));
        }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        TerrainAsset terrain = Assert.Single(reopened.Document.Terrains);
        Assert.Equal(id, terrain.Id);
        Assert.Equal(2000u, terrain.MapDataId);
        Assert.Equal((3, 2), (terrain.Width, terrain.Height));
        Assert.Equal("Forest entrance", terrain.Name);
        Assert.Equal(initial, reopened.ReadTerrainCell(id, 0, 0));
        Assert.Equal(updated, reopened.ReadTerrainCell(id, 2, 1));
        string input = directory.PathFor("project", "Inputs", terrain.CellDigest + ".octerrain");
        byte[] data = File.ReadAllBytes(input);
        Assert.Equal(36, data.Length);
        Assert.Equal(updated.SurfaceId, BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(30)));
        Assert.Equal(updated.PassabilityFlag, BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(32)));
        Assert.Equal(updated.Elevation, BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(34)));
    }

    [Fact]
    public void IdenticalCellEditKeepsRedoAndDoesNotWriteAnotherRevision()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid id = project.CreateTerrain("Field", 3000, 2, 2, new TerrainCell(0, 0, 0));
        project.Save(TestContext.Current.CancellationToken);
        ProjectDocument saved = project.Document;
        byte[] index = File.ReadAllBytes(directory.Manifest);

        project.EditTerrainCell(id, 1, 1, new TerrainCell(0, 0, 0), "Field");
        Assert.Same(saved, project.Document);
        Assert.False(project.IsModified);
        project.Rename("Temporary");
        project.Undo();
        Assert.True(project.CanRedo);
        project.EditTerrainCell(id, 1, 1, new TerrainCell(0, 0, 0), "Field");
        Assert.Same(saved, project.Document);
        Assert.True(project.CanRedo);
        Assert.False(project.IsModified);
        project.Save(TestContext.Current.CancellationToken);
        Assert.Equal(index, File.ReadAllBytes(directory.Manifest));
    }

    [Fact]
    public void RejectsInvalidCoordinatesIdentifiersAndBudgetsWithoutChangingProject()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid id = project.CreateTerrain("Field", 4000, 4, 3, new TerrainCell(0, 0, 0));
        project.Save(TestContext.Current.CancellationToken);
        ProjectDocument original = project.Document;
        Assert.Throws<InvalidDataException>(() => project.EditTerrainCell(id, 4, 0, new TerrainCell(2, 1, 1), "Changed"));
        Assert.Throws<InvalidDataException>(() => project.EditTerrainCell(id, 0, -1, new TerrainCell(2, 1, 1), "Changed"));
        Assert.Throws<InvalidDataException>(() => project.CreateTerrain("Conflict", 4000, 1, 1, new TerrainCell(0, 0, 0)));
        Assert.Throws<InvalidDataException>(() => project.CreateTerrain("Oversized", 4001, 1024, 1024, new TerrainCell(0, 0, 0)));
        Assert.Throws<InvalidDataException>(() => project.EditTerrainCell(id, 1, 1, new TerrainCell(1, 2, 3), ""));
        Assert.Same(original, project.Document);
        Assert.False(project.IsModified);
        Assert.Equal(new TerrainCell(0, 0, 0), project.ReadTerrainCell(id, 1, 1));
    }

    [Fact]
    public void MissingOrCorruptedCellPayloadRejectsOpenWithoutChangingIndex()
    {
        using ProjectDirectory directory = new();
        string payload;
        byte[] savedIndex;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            Guid id = project.CreateTerrain("Field", 5000, 2, 2, new TerrainCell(1, 2, 3));
            project.EditTerrainCell(id, 1, 1, new TerrainCell(9, 8, 7), "Field");
            project.Save(TestContext.Current.CancellationToken);
            payload = directory.PathFor("project", "Inputs", Assert.Single(project.Document.Terrains).CellDigest + ".octerrain");
            savedIndex = File.ReadAllBytes(directory.Manifest);
        }

        byte[] original = File.ReadAllBytes(payload);
        byte[] damaged = (byte[])original.Clone();
        damaged[^1] ^= 1;
        File.WriteAllBytes(payload, damaged);
        Assert.Throws<InvalidDataException>(() => StudioProject.Open(directory.Project, TestContext.Current.CancellationToken));
        Assert.Equal(savedIndex, File.ReadAllBytes(directory.Manifest));

        File.WriteAllBytes(payload, original[..^1]);
        Assert.Throws<InvalidDataException>(() => StudioProject.Open(directory.Project, TestContext.Current.CancellationToken));
        File.Delete(payload);
        Assert.ThrowsAny<IOException>(() => StudioProject.Open(directory.Project, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void BuildRejectsAuthoredTerrainBeforePublishingAnyOutput()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        project.CreateTerrain("Field", 6000, 2, 2, new TerrainCell(0, 0, 0));
        project.Save(TestContext.Current.CancellationToken);
        string destination = directory.PathFor("build");
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            StudioCompiler.Compile(project, destination, TestContext.Current.CancellationToken));
        Assert.Contains("server terrain", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(destination));
        Assert.Empty(Directory.GetDirectories(directory.Root, ".studio-build-*"));
    }

    [Fact]
    public void DeletedTerrainCanBeRestoredWithUndo()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid id = project.CreateTerrain("Field", 7000, 2, 2, new TerrainCell(1, 2, 3));
        project.Save(TestContext.Current.CancellationToken);
        project.Delete(id);
        Assert.Empty(project.Document.Terrains);
        project.Undo();
        Assert.Equal(new TerrainCell(1, 2, 3), project.ReadTerrainCell(id, 1, 1));
        Assert.False(project.IsModified);
    }
}
