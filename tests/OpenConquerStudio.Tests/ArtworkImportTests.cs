using OpenConquerStudio.Authoring;
using OpenConquerStudio.ConquerImport;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio.Tests;

public sealed class ArtworkImportTests
{
    [Theory]
    [InlineData("row", 166, 25)]
    [InlineData("row-highlighted", 166, 25)]
    [InlineData("save-dialog", 344, 192)]
    [InlineData("clear-dialog", 296, 192)]
    [InlineData("ok-over", 96, 26)]
    [InlineData("ok-down", 96, 26)]
    [InlineData("cancel-over", 96, 26)]
    [InlineData("cancel-down", 96, 26)]
    public void ImportExistingArtwork_EditSaveAndCompilePreservesPixels(string suffix, int width, int height)
    {
        using ProjectDirectory directory = new();
        string key = "conquer/login/account/" + suffix;
        Guid id;
        byte[] pixels;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game artwork"))
        {
            id = ConquerArtworkImporter.Import(project, key, TestContext.Current.CancellationToken);
            TextureData source = project.Preview(id, TestContext.Current.CancellationToken);
            Assert.Equal(width, source.Width);
            Assert.Equal(height, source.Height);
            project.EditTexture(id, "Authored replacement", flipHorizontal: true, clockwiseQuarterTurns: 1);
            pixels = project.Preview(id, TestContext.Current.CancellationToken).Pixels.ToArray();
            Assert.Equal(source.Transform(true, 1, TestContext.Current.CancellationToken).Pixels.ToArray(), pixels);
            project.Save(TestContext.Current.CancellationToken);
        }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        TextureAsset asset = Assert.Single(reopened.Document.Assets);
        Assert.Equal(key, asset.SourceKey);
        Assert.Equal("rgba", asset.SourceFormat);
        Assert.Equal(id, asset.Id);
        ContentBuild build = StudioCompiler.Compile(reopened, directory.PathFor("build"), TestContext.Current.CancellationToken);
        Assert.Equal(height, Assert.Single(build.Textures).Width);
        using FileStream input = File.OpenRead(directory.PathFor("build", id.ToString("N") + ".octex"));
        (_, TextureData compiled) = TextureCodec.Read(input);
        Assert.Equal(pixels, compiled.Pixels.ToArray());
    }

    [Fact]
    public void UnknownArtwork_LeavesProjectUnchanged()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Assert.Throws<InvalidDataException>(() => ConquerArtworkImporter.Import(project, "unsupported", TestContext.Current.CancellationToken));
        Assert.Empty(project.Document.Assets);
        Assert.False(project.IsModified);
    }

    [Fact]
    public void ReplaceArtwork_PreservesIdentitySourceAssociationAndEdits()
    {
        using ProjectDirectory directory = new();
        Guid id;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            id = ConquerArtworkImporter.Import(project, "conquer/login/account/row", TestContext.Current.CancellationToken);
            project.EditTexture(id, "New artwork", flipHorizontal: true, clockwiseQuarterTurns: 1);
            project.Save(TestContext.Current.CancellationToken);
            project.ReplaceTexture(id, directory.WriteTga(), TestContext.Current.CancellationToken);
            Assert.Equal(id, Assert.Single(project.Document.Assets).Id);
            project.Save(TestContext.Current.CancellationToken);
            Assert.Throws<InvalidDataException>(() => project.ReplaceTexture(id, directory.PathFor("unsupported.png"), TestContext.Current.CancellationToken));
            Assert.False(project.IsModified);
        }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        TextureAsset asset = Assert.Single(reopened.Document.Assets);
        Assert.Equal(id, asset.Id);
        Assert.Equal("New artwork", asset.Name);
        Assert.Equal("conquer/login/account/row", asset.SourceKey);
        Assert.Equal("tga", asset.SourceFormat);
        Assert.True(asset.FlipHorizontal);
        Assert.Equal(1, asset.ClockwiseQuarterTurns);
        Assert.Equal(new byte[] { 0, 255, 0, 255, 255, 0, 0, 255 }, reopened.Preview(id, TestContext.Current.CancellationToken).Pixels.ToArray());
    }

    [Fact]
    public void Authoring_DoesNotDependOnGameApplicationsOrArtworkAdapter()
    {
        string[] dependencies = typeof(StudioProject).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();
        Assert.DoesNotContain("OpenConquer.Client", dependencies);
        Assert.DoesNotContain("OpenConquer.Launcher", dependencies);
        Assert.DoesNotContain("OpenConquer.Networking", dependencies);
        Assert.DoesNotContain("OpenConquerStudio.ConquerImport", dependencies);
        Assert.DoesNotContain(dependencies, name => name.StartsWith("Avalonia", StringComparison.Ordinal));
    }
}
