using OpenConquerStudio.Authoring;

namespace OpenConquerStudio.Tests;

public sealed class ServerTerrainBuildTests
{
    [Fact]
    public async Task InvalidToolLeavesSavedProjectAndBuildDirectoryUnchanged()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "World");
        Guid id = project.CreateTerrain("Field", 900001, 2, 2, new TerrainCell(1, 0, 0));
        project.Save(TestContext.Current.CancellationToken);
        byte[] index = File.ReadAllBytes(directory.Manifest);
        string destination = directory.PathFor("server-output");

        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await ServerTerrainBuild.BuildAsync(project, id, directory.PathFor("missing", "OpenConquer.GameData.Tool.dll"),
                destination, TestContext.Current.CancellationToken));

        Assert.Equal(index, File.ReadAllBytes(directory.Manifest));
        Assert.False(project.IsModified);
        Assert.False(Directory.Exists(destination));
        Assert.Empty(Directory.EnumerateDirectories(directory.Root, ".studio-terrain-*"));
    }

    [Fact]
    public async Task ToolFailureCleansSourceAndTemporaryOutputWithoutPublishing()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "World");
        Guid id = project.CreateTerrain("Field", 900001, 1, 1, new TerrainCell(0, 0, 0));
        project.Save(TestContext.Current.CancellationToken);
        string tool = directory.PathFor("OpenConquer.GameData.Tool.dll");
        File.WriteAllText(tool, "Not an executable assembly");
        string destination = directory.PathFor("server-output");
        byte[] index = File.ReadAllBytes(directory.Manifest);

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await ServerTerrainBuild.BuildAsync(project, id, tool, destination, TestContext.Current.CancellationToken));

        Assert.False(Directory.Exists(destination));
        Assert.Empty(Directory.EnumerateDirectories(directory.Root, ".studio-terrain-*"));
        Assert.Equal(index, File.ReadAllBytes(directory.Manifest));
    }

    [Fact]
    public async Task RejectedDestinationAndCancellationCannotPublish()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "World");
        Guid id = project.CreateTerrain("Field", 900001, 1, 1, new TerrainCell(0, 0, 0));
        project.Save(TestContext.Current.CancellationToken);
        string tool = directory.PathFor("OpenConquer.GameData.Tool.dll");
        File.WriteAllText(tool, "Not an executable assembly");
        string destination = directory.PathFor("server-output");
        Directory.CreateDirectory(destination);
        string sentinel = Path.Combine(destination, "keep.txt");
        File.WriteAllText(sentinel, "unchanged");

        await Assert.ThrowsAsync<IOException>(async () =>
            await ServerTerrainBuild.BuildAsync(project, id, tool, destination, TestContext.Current.CancellationToken));
        Assert.Equal("unchanged", File.ReadAllText(sentinel));

        using CancellationTokenSource canceled = new();
        canceled.Cancel();
        string unused = directory.PathFor("unused");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await ServerTerrainBuild.BuildAsync(project, id, tool, unused, canceled.Token));
        Assert.False(Directory.Exists(unused));
        Assert.Empty(Directory.EnumerateDirectories(directory.Root, ".studio-terrain-*"));
    }

    [Fact]
    public async Task InvalidAssetAndUnsavedEditsAreRejectedBeforeLaunchingTheTool()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "World");
        Guid id = project.CreateTerrain("Field", 900001, 2, 1, new TerrainCell(1, 0, 0));
        string missingTool = directory.PathFor("missing.dll");
        string destination = directory.PathFor("output");
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ServerTerrainBuild.BuildAsync(project, id, missingTool, destination, TestContext.Current.CancellationToken));

        project.Save(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await ServerTerrainBuild.BuildAsync(project, Guid.NewGuid(), missingTool, destination,
                TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(destination));
    }
}
