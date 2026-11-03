using OpenConquerStudio.Authoring;
using OpenConquerStudio.ConquerImport;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio.Tests;

public sealed class ClientProjectTests
{
    [Fact]
    public void ConnectedClient_ImportsRealAniAndCompilesAnIndependentRuntimeGraph()
    {
        using ProjectDirectory directory = new();
        Guid clip;
        string content = Path.Combine(ProjectDirectory.RepositoryRoot, "content", "retail-5517", "payload");
        byte[] sourceBefore = File.ReadAllBytes(Path.Combine(content, "ani", "control.ani"));
        using (StudioProject project = StudioProject.Create(directory.Project, "Client assets"))
        {
            ClientProjectConnection connection = ClientProjectConnection.Attach(project, ProjectDirectory.RepositoryRoot, content, TestContext.Current.CancellationToken);
            var entries = connection.Inventory(TestContext.Current.CancellationToken);
            Assert.Contains(entries, entry => entry.MapDataId == 1010 && entry.Available);
            Assert.Contains(entries, entry => entry.Kind == "puzzle" && entry.Path.EndsWith("newbie.pul", StringComparison.Ordinal));
            var frames = connection.ReadAnimation("ani/control.ani", "Action_Act0Btn", TestContext.Current.CancellationToken);
            Assert.Equal(2, frames.Length);
            clip = project.ImportAnimation("Action states", frames, 125);
            Assert.Equal(5, project.Document.Resources.Count());
            project.Save(TestContext.Current.CancellationToken);
            StudioCompiler.Compile(project, directory.PathFor("build"), TestContext.Current.CancellationToken);
        }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        ClientProjectConnection attached = Assert.IsType<ClientProjectConnection>(ClientProjectConnection.OpenAttached(reopened.Root));
        Assert.Equal(content, attached.ContentRoot);
        ContentPackage package = ContentPackage.Load(directory.PathFor("build"), TestContext.Current.CancellationToken);
        Assert.Equal(1, package.Sprites.Animations[clip].SampleTicks(125));
        Assert.Equal(sourceBefore, File.ReadAllBytes(Path.Combine(content, "ani", "control.ani")));
        var map = attached.ReadMap(1010);
        Assert.Equal(132, map.CellGrid.Width);
        Assert.Equal(132, map.CellGrid.Height);
        Assert.Equal(38, map.Scene.Scenery.Count);
    }

    [Fact]
    public void MissingSourceFrames_DoNotCreatePartialAuthoringResources()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Client assets");
        ClientProjectConnection connection = ClientProjectConnection.Attach(project, ProjectDirectory.RepositoryRoot, Path.Combine(ProjectDirectory.RepositoryRoot, "content", "retail-5517", "payload"), TestContext.Current.CancellationToken);
        Assert.Throws<FileNotFoundException>(() => connection.ReadAnimation("ani/control.ani", "Dialog2", TestContext.Current.CancellationToken));
        Assert.Empty(project.Document.Resources);
        Assert.False(project.IsModified);
    }
}
