using System.Collections.Immutable;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio.Tests;

public sealed class SourceFramePromotionTests
{
    [Fact]
    public void SelectedFramesBecomeStableTypedTexturesAndBuildOfflineAfterReopen()
    {
        using ProjectDirectory directory = new();
        Guid terrainId;
        ImmutableArray<Guid> textures;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            terrainId = project.ImportTerrain(OwnedMapVisualInputsTests.Input(), "Ground");
            textures = project.PromoteSourceFrames(terrainId,
                ["images/scene.tga", "images/shared.tga"], TestContext.Current.CancellationToken);
            Assert.Equal(2, textures.Length);
            Assert.Equal(2, project.Document.Assets.Length);
            TerrainAsset terrain = Assert.Single(project.Document.Terrains);
            Assert.Equal(textures[0], terrain.Source!.Frames.Single(f => f.Path == "images/scene.tga").TextureId);
            Assert.Equal(textures[1], terrain.Source.Frames.Single(f => f.Path == "images/shared.tga").TextureId);
            Assert.Equal(["terrain"], project.Document.Dependents(textures[0]).Select(x => x.Kind).ToArray());
            Assert.Equal(new[] { textures[0], textures[1] }.Order().ToArray(),
                project.Document.Dependencies(terrainId).ToArray());
            Assert.Equal(new byte[] { 21, 0, 0, 255 },
                project.Preview(textures[1], TestContext.Current.CancellationToken).Pixels.ToArray());
            project.Save(TestContext.Current.CancellationToken);
        }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        Assert.Equal(textures[0], reopened.Document.Terrains.Single().Source!.Frames[1].TextureId);
        ImmutableArray<Guid> repeated = reopened.PromoteSourceFrames(terrainId,
            ["images/scene.tga", "images/shared.tga"], TestContext.Current.CancellationToken);
        Assert.Equal(textures.ToArray(), repeated.ToArray());
        Assert.False(reopened.IsModified);
        Assert.Throws<InvalidOperationException>(() => StudioCompiler.Compile(reopened,
            directory.PathFor("unsupported-world-build"), TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(directory.PathFor("unsupported-world-build")));

        reopened.UnlinkSourceFrame(terrainId, "images/scene.tga");
        reopened.UnlinkSourceFrame(terrainId, "images/shared.tga");
        reopened.Delete(terrainId);
        reopened.Save(TestContext.Current.CancellationToken);
        ContentBuild build = StudioCompiler.Compile(reopened, directory.PathFor("compiled"),
            TestContext.Current.CancellationToken);
        Assert.Equal(2, build.Textures.Length);
        Assert.All(build.Textures, t => Assert.Contains(t.Id, textures));
        _ = StudioCompiler.Validate(directory.PathFor("compiled"), TestContext.Current.CancellationToken);
    }

    [Fact]
    public void BatchFailureDoesNotPromoteAnyFramesOrDestroyRedo()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid terrain = project.ImportTerrain(OwnedMapVisualInputsTests.Input(), "Ground");
        project.Save(TestContext.Current.CancellationToken);
        project.Rename("Temporary");
        project.Undo();
        Assert.True(project.CanRedo);
        ProjectDocument saved = project.Document;

        Assert.Throws<InvalidDataException>(() => project.PromoteSourceFrames(terrain,
            ["images/shared.tga", "images/missing.tga"], TestContext.Current.CancellationToken));
        Assert.Throws<InvalidDataException>(() => project.PromoteSourceFrames(terrain,
            ["images/shared.tga", "images/shared.tga"], TestContext.Current.CancellationToken));
        Assert.Same(saved, project.Document);
        Assert.Empty(project.Document.Assets);
        Assert.True(project.CanRedo);
        Assert.False(project.IsModified);
    }

    [Fact]
    public void PromotionBudgetFailureIsAtomic()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid terrain = project.ImportTerrain(OwnedMapVisualInputsTests.Input(), "Ground");
        for (int i = 0; i < ProjectDocument.MaximumAssets; i++)
        {
            project.CreateTexture("Existing " + i, 1, 1, 1, 2, 3);
        }
        ProjectDocument before = project.Document;
        Assert.Throws<InvalidDataException>(() => project.PromoteSourceFrames(terrain,
            ["images/shared.tga"], TestContext.Current.CancellationToken));
        Assert.Same(before, project.Document);
        Assert.All(project.Document.Terrains.Single().Source!.Frames, frame => Assert.Null(frame.TextureId));
    }

    [Fact]
    public void RefreshRetainsLinkedGuidAndAuthoredTransformsWhileUpdatingPixels()
    {
        using ProjectDirectory directory = new();
        Guid terrainId;
        Guid textureId;
        Guid atlasId;
        Guid spriteId;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            terrainId = project.ImportTerrain(OwnedMapVisualInputsTests.Input(), "Ground");
            textureId = Assert.Single(project.PromoteSourceFrames(terrainId,
                ["images/shared.tga"], TestContext.Current.CancellationToken));
            atlasId = project.CreateAtlas("Atlas", textureId);
            spriteId = project.Document.Atlases.Single().Sprites.Single().Id;
            project.EditTexture(textureId, "Authored frame", true, 2);
            project.Save(TestContext.Current.CancellationToken);
            ProjectDocument saved = project.Document;

            TerrainImport changed = OwnedMapVisualInputsTests.Input() with
            {
                FrameFiles = [new("images/shared.tga", OwnedMapVisualInputsTests.Tga(77)),
                    new("images/scene.tga", OwnedMapVisualInputsTests.Tga(42))]
            };
            project.RefreshTerrain(terrainId, changed);
            TextureAsset updated = Assert.Single(project.Document.Assets);
            Assert.Equal(textureId, updated.Id);
            Assert.Equal("Authored frame", updated.Name);
            Assert.True(updated.FlipHorizontal);
            Assert.Equal(2, updated.ClockwiseQuarterTurns);
            Assert.Equal(atlasId, project.Document.Atlases.Single().Id);
            Assert.Equal(spriteId, project.Document.Atlases.Single().Sprites.Single().Id);
            Assert.Equal(77, project.Preview(textureId, TestContext.Current.CancellationToken).Pixels.Span[0]);
            Assert.Equal(textureId, project.Document.Terrains.Single().Source!.Frames[0].TextureId);

            project.Undo();
            Assert.Same(saved, project.Document);
            Assert.Equal(21, project.Preview(textureId, TestContext.Current.CancellationToken).Pixels.Span[0]);
            Assert.True(project.CanRedo);
            project.Redo();
            project.Save(TestContext.Current.CancellationToken);
        }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        Assert.Equal(textureId, reopened.Document.Terrains.Single().Source!.Frames[0].TextureId);
        Assert.Equal(77, reopened.Preview(textureId, TestContext.Current.CancellationToken).Pixels.Span[0]);
        Assert.Equal(atlasId, reopened.Document.Atlases.Single().Id);
    }

    [Fact]
    public void UnchangedRefreshIsNoOpAndDoesNotClearRedo()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid terrain = project.ImportTerrain(OwnedMapVisualInputsTests.Input(), "Ground");
        Guid id = Assert.Single(project.PromoteSourceFrames(terrain,
            ["images/shared.tga"], TestContext.Current.CancellationToken));
        project.Save(TestContext.Current.CancellationToken);
        ProjectDocument saved = project.Document;
        project.Rename("Temporary");
        project.Undo();
        project.RefreshTerrain(terrain, OwnedMapVisualInputsTests.Input());
        Assert.Same(saved, project.Document);
        Assert.False(project.IsModified);
        Assert.True(project.CanRedo);
        Assert.Equal(id, project.Document.Terrains.Single().Source!.Frames[0].TextureId);
    }

    [Fact]
    public void LinkedFrameBlocksDeletionAndReplacementUntilExplicitlyUnlinked()
    {
        using ProjectDirectory directory = new();
        string replacement = directory.WriteTga();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid terrain = project.ImportTerrain(OwnedMapVisualInputsTests.Input(), "Ground");
        Guid id = Assert.Single(project.PromoteSourceFrames(terrain,
            ["images/shared.tga"], TestContext.Current.CancellationToken));
        project.Save(TestContext.Current.CancellationToken);
        ProjectDocument saved = project.Document;

        Assert.Throws<InvalidDataException>(() => project.Delete(id));
        Assert.Throws<InvalidDataException>(() => project.ReplaceTexture(id, replacement,
            TestContext.Current.CancellationToken));
        Assert.Same(saved, project.Document);

        project.UnlinkSourceFrame(terrain, "images/shared.tga");
        Assert.Null(project.Document.Terrains.Single().Source!.Frames[0].TextureId);
        project.ReplaceTexture(id, replacement, TestContext.Current.CancellationToken);
        Assert.NotEqual(saved.Assets[0].SourceDigest, project.Document.Assets[0].SourceDigest);
        project.Undo();
        project.Undo();
        Assert.Same(saved, project.Document);
        Assert.True(project.CanRedo);
    }
}
