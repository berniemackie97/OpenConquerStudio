using OpenConquerStudio.Authoring;
using OpenConquerStudio.Tests;

namespace OpenConquerStudio.App.Tests;

public sealed class StudioStartupProjectTests
{
    [Fact]
    public void LaunchFromRiderProjectDirectory_FindsContainingClientCheckout()
    {
        string repository = ProjectDirectory.RepositoryRoot;
        string root = Assert.IsType<string>(StudioStartupProject.Resolve(null,
            Path.Combine(repository, "src", "OpenConquer.Client"), AppContext.BaseDirectory));
        Assert.Equal(repository, root);
    }

    [Fact]
    public void AuthoredProjectInWorkingDirectory_WinsOverApplicationCheckout()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Other game");
        string nested = directory.PathFor("project", "Assets");
        Directory.CreateDirectory(nested);
        Assert.Equal(directory.Project, StudioStartupProject.Resolve(null, nested, AppContext.BaseDirectory));
    }

    [Fact]
    public void ExplicitProject_WinsAndIsNotReplacedWhenMissing()
    {
        using ProjectDirectory directory = new();
        Assert.Equal(directory.PathFor("selected-game"), StudioStartupProject.Resolve("selected-game", directory.Root, AppContext.BaseDirectory));
    }

    [Fact]
    public void UnrelatedWorkingDirectory_UsesApplicationCheckout()
    {
        using ProjectDirectory directory = new();
        string client = ProjectDirectory.RepositoryRoot;
        Assert.Equal(client, StudioStartupProject.Resolve(null, directory.Root,
            Path.Combine(client, "src", "OpenConquer.Client")));
    }

    [Fact]
    public void StandaloneApplicationWithoutProject_KeepsWelcomeWorkflow()
    {
        using ProjectDirectory directory = new();
        Assert.Null(StudioStartupProject.Resolve(null, directory.Root, directory.Project));
    }

    [Fact]
    public void StudioSourceCheckout_DoesNotSelectItsClientDependencyAsTheGame()
    {
        using ProjectDirectory directory = new();
        Assert.Null(StudioStartupProject.Resolve(null, directory.Root, AppContext.BaseDirectory));
    }
}
