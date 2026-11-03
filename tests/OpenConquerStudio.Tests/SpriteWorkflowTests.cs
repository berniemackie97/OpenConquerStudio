using System.Text.Json;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio.Tests;

public sealed class SpriteWorkflowTests
{
    private static readonly JsonSerializerOptions s_json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    [Fact]
    public void AuthorSaveReopenCompileAndPlay_PreservesGraphAndExactFrameBoundaries()
    {
        using ProjectDirectory directory = new();
        Guid animationId;
        Guid atlasId;
        Guid textureId;
        using (StudioProject project = StudioProject.Create(directory.Project, "Animated game"))
        {
            textureId = project.ImportTexture(directory.WriteTga(), "Two colors", TestContext.Current.CancellationToken);
            atlasId = project.CreateAtlas("Palette", textureId);
            AtlasAsset atlas = project.Document.Atlases[0];
            SpriteRegion red = atlas.Sprites[0] with
            {
                Name = "Red",
                Width = 1,
                PivotX = 0,
                PivotY = 0
            };
            SpriteRegion green = red with
            {
                Id = Guid.NewGuid(),
                Name = "Green",
                X = 1
            };
            project.EditAtlas(atlas with
            {
                Sprites = [red, green]
            });
            animationId = project.CreateAnimation("Blink", atlasId, 1000, 100);
            AnimationAsset animation = project.Document.Animations[0];
            project.EditAnimation(animation with
            {
                Frames = animation.Frames.SetItem(1, animation.Frames[1] with
                {
                    DurationTicks = 250
                })
            });
            Assert.Equal(new[] { atlasId, animationId }.Order(), project.Document.Dependents(textureId).Select(a => a.Id).Order());
            project.Save(TestContext.Current.CancellationToken);
            StudioCompiler.Compile(project, directory.PathFor("first"), TestContext.Current.CancellationToken);
        }

        using (StudioProject project = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken))
        {
            Assert.Equal(textureId, project.Document.Atlases[0].TextureId);
            Assert.Equal(atlasId, project.Document.Animations[0].Frames[0].AtlasId);
            StudioCompiler.Compile(project, directory.PathFor("second"), TestContext.Current.CancellationToken);
        }

        foreach (string path in Directory.GetFiles(directory.PathFor("first")))
        {
            Assert.Equal(File.ReadAllBytes(path), File.ReadAllBytes(directory.PathFor("second", Path.GetFileName(path))));
        }

        ContentPackage package = ContentPackage.Load(directory.PathFor("second"), TestContext.Current.CancellationToken);
        Assert.Equal("Red", package.Sprites.Sample(animationId, 99).Sprite.Name);
        Assert.Equal("Green", package.Sprites.Sample(animationId, 100).Sprite.Name);
        Assert.Equal("Green", package.Sprites.Sample(animationId, 349).Sprite.Name);
        Assert.Equal("Red", package.Sprites.Sample(animationId, 350).Sprite.Name);
        Assert.Equal(1, package.Sprites.Sample(animationId, 100).Sprite.X);
        Assert.Equal(new byte[] { 255, 0, 0, 255, 0, 255, 0, 255 }, package.Sprites.Textures[textureId].Pixels.ToArray());
    }

    [Fact]
    public void GraphEdits_AreAtomicAndBlockBrokenReferencesAndDependentDeletion()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid texture = project.CreateTexture("Texture", 2, 1, 1, 2, 3);
        Guid atlasId = project.CreateAtlas("Atlas", texture);
        Guid animationId = project.CreateAnimation("Clip", atlasId);
        ProjectDocument before = project.Document;
        Assert.Throws<InvalidDataException>(() => project.Delete(texture));
        Assert.Throws<InvalidDataException>(() => project.Delete(atlasId));
        Assert.Throws<InvalidDataException>(() => project.EditTexture(texture, "Shrunk", false, 1));
        AtlasAsset atlas = before.Atlases[0];
        Assert.Throws<InvalidDataException>(() => project.EditAtlas(atlas with { Sprites = [] }));
        Assert.Throws<InvalidDataException>(() => project.EditAtlas(atlas with { TextureId = animationId }));
        Assert.Throws<InvalidDataException>(() => project.EditAtlas(atlas with { Sprites = [atlas.Sprites[0] with { Id = Guid.NewGuid() }] }));
        Assert.Throws<InvalidDataException>(() => project.EditAnimation(before.Animations[0] with { Frames = [] }));
        Assert.Same(before, project.Document);
        project.Delete(animationId);
        project.Delete(atlasId);
        project.Delete(texture);
        Assert.Empty(project.Document.Resources);
        project.Undo();
        project.Undo();
        project.Undo();
        Assert.Equal(3, project.Document.Resources.Count());
    }

    [Fact]
    public void UndoRedo_AcrossSourceReplacementAndSave_RestoresPixelsAndSavedState()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid texture = project.CreateTexture("Blue", 1, 1, 0, 0, 255);
        project.Save(TestContext.Current.CancellationToken);
        project.ReplaceTexture(texture, directory.WriteTga(), TestContext.Current.CancellationToken);
        project.Undo();
        Assert.False(project.IsModified);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, project.Preview(texture, TestContext.Current.CancellationToken).Pixels.ToArray());
        project.Redo();
        Assert.True(project.IsModified);
        Assert.Equal(2, project.Preview(texture, TestContext.Current.CancellationToken).Width);
        project.Save(TestContext.Current.CancellationToken);
        project.Undo();
        Assert.True(project.IsModified);
        project.Redo();
        Assert.False(project.IsModified);
        project.Rename("Branched edit");
        Assert.False(project.CanRedo);
    }


    [Fact]
    public void SourceReimportPreservesResourceGraphAndSavedStateWhenUnchanged()
    {
        using ProjectDirectory directory = new();
        Guid texture;
        byte[] replacementPixels = [90, 80, 70, 255, 60, 50, 40, 255];
        string replacementDigest = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(replacementPixels));
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            byte[] pixels = [10, 20, 30, 255, 40, 50, 60, 255];
            TextureData originalPixels = new(2, 1, pixels);
            string originalDigest = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(pixels));
            ImportedImageFrame original = new("Source", originalPixels, "rgba", originalDigest, "images/item");
            texture = project.ImportSourceImage(original);
            Guid atlas = project.CreateAtlas("Atlas", texture);
            Guid animation = project.CreateAnimation("Animation", atlas);
            Guid sprite = project.Document.Atlases.Single().Sprites.Single().Id;
            Guid frame = project.Document.Animations.Single().Frames.Single().Id;
            project.Save(TestContext.Current.CancellationToken);
            ProjectDocument saved = project.Document;

            project.ReplaceSourceImage(texture, original with { Texture = new TextureData(2, 1, pixels) });
            Assert.Same(saved, project.Document);
            Assert.False(project.IsModified);
            Assert.False(project.CanRedo);

            project.Rename("Changed");
            project.Undo();
            Assert.True(project.CanRedo);
            project.ReplaceSourceImage(texture, original);
            Assert.True(project.CanRedo);
            Assert.Same(saved, project.Document);

            project.ReplaceSourceImage(texture, original with
            {
                Texture = new TextureData(2, 1, replacementPixels),
                SourceDigest = replacementDigest
            });

            Assert.False(project.CanRedo);
            Assert.True(project.IsModified);
            Assert.Equal(texture, project.Document.Atlases.Single().TextureId);
            Assert.Equal(atlas, project.Document.Animations.Single().Frames.Single().AtlasId);
            Assert.Equal(animation, project.Document.Animations.Single().Id);
            Assert.Equal(sprite, project.Document.Atlases.Single().Sprites.Single().Id);
            Assert.Equal(frame, project.Document.Animations.Single().Frames.Single().Id);
            Assert.Equal(replacementPixels, project.Preview(texture).Pixels.ToArray());

            project.Undo();
            Assert.Same(saved, project.Document);
            Assert.False(project.IsModified);
            Assert.Equal(pixels, project.Preview(texture).Pixels.ToArray());
            project.Redo();
            project.Save(TestContext.Current.CancellationToken);
        }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        Assert.Equal(texture, Assert.Single(reopened.Document.Assets).Id);
        Assert.Equal(replacementDigest, reopened.Document.Assets[0].SourceDigest);
        Assert.Equal("images/item", reopened.Document.Assets[0].SourceKey);
        Assert.Equal(replacementPixels, reopened.Preview(texture).Pixels.ToArray());
    }

    [Fact]
    public void InvalidSourceReimportDoesNotChangeResourcesOrHistory()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid texture = project.CreateTexture("Texture", 2, 1, 1, 2, 3);
        project.CreateAtlas("Atlas", texture);
        project.Save(TestContext.Current.CancellationToken);
        byte[] manifest = File.ReadAllBytes(directory.Manifest);
        ProjectDocument before = project.Document;
        byte[] pixels = [10, 20, 30, 255];
        string digest = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(pixels));
        ImportedImageFrame reduced = new("Smaller", new TextureData(1, 1, pixels), "rgba", digest, "images/item");
        Assert.Throws<InvalidDataException>(() => project.ReplaceSourceImage(texture, reduced));
        Assert.Same(before, project.Document);
        Assert.False(project.IsModified);
        Assert.Equal(manifest, File.ReadAllBytes(directory.Manifest));

        ImportedImageFrame invalidKey = reduced with { Texture = new TextureData(2, 1, [10, 20, 30, 255, 10, 20, 30, 255]), SourceKey = "../invalid" };
        Assert.Throws<InvalidDataException>(() => project.ReplaceSourceImage(texture, invalidKey));
        Assert.Same(before, project.Document);
        Assert.False(project.IsModified);
    }

    [Fact]
    public void FailedRevisionPublication_ReopensLastCommittedGraph()
    {
        using ProjectDirectory directory = new();
        byte[] committed;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            Guid texture = project.CreateTexture("Texture", 1, 1, 1, 2, 3);
            project.Save(TestContext.Current.CancellationToken);
            committed = File.ReadAllBytes(directory.Manifest);
            Guid atlas = project.CreateAtlas("Uncommitted", texture);
            Directory.CreateDirectory(directory.PathFor("project", "Assets", atlas.ToString("N")));
            Directory.Move(directory.PathFor("project", "Assets", atlas.ToString("N")), directory.PathFor("blocked"));
            File.WriteAllText(directory.PathFor("project", "Assets", atlas.ToString("N")), "blocked");
            Assert.Throws<IOException>(() => project.Save(TestContext.Current.CancellationToken));
            Assert.Equal(committed, File.ReadAllBytes(directory.Manifest));
            Assert.True(project.IsModified);
        }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        Assert.Single(reopened.Document.Resources);
        Assert.Empty(reopened.Document.Atlases);
    }

    [Fact]
    public void MigrationCopiesVersionOne_PreservesIdentityAndLeavesSourceBytesUntouched()
    {
        using ProjectDirectory directory = new();
        Guid projectId = Guid.NewGuid();
        Guid textureId = Guid.NewGuid();
        byte[] pixels = [1, 2, 3, 255];
        string digest = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(pixels));
        TextureAsset texture = new(textureId, "Original", 1, 1, digest, "created", digest, true, 2);
        byte[] manifest = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            id = projectId,
            name = "Existing game",
            assets = new[] { texture }
        }, s_json);
        File.WriteAllBytes(directory.Manifest, manifest);
        Directory.CreateDirectory(directory.PathFor("project", "Inputs"));
        File.WriteAllBytes(directory.PathFor("project", "Inputs", digest + ".rgba"), pixels);
        Assert.Contains("migration", Assert.Throws<InvalidDataException>(() => StudioProject.Open(directory.Project, TestContext.Current.CancellationToken)).Message, StringComparison.OrdinalIgnoreCase);
        Directory.CreateDirectory(directory.PathFor("migrated"));
        using StudioProject migrated = StudioProject.Migrate(directory.Project, directory.PathFor("migrated"), TestContext.Current.CancellationToken);
        Assert.Equal(projectId, migrated.Document.Id);
        Assert.Equal(texture, Assert.Single(migrated.Document.Assets));
        Assert.Equal(manifest, File.ReadAllBytes(directory.Manifest));
        Assert.Equal(pixels, File.ReadAllBytes(directory.PathFor("project", "Inputs", digest + ".rgba")));
        Assert.False(migrated.IsModified);
    }

    [Fact]
    public void RevisionsAreImmutable_ExternalCorruptionBlocksSaveAndOpen()
    {
        using ProjectDirectory directory = new();
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            project.CreateTexture("Texture", 1, 1, 1, 2, 3);
            project.Save(TestContext.Current.CancellationToken);
            byte[] index = File.ReadAllBytes(directory.Manifest);
            string revision = Assert.Single(Directory.GetFiles(directory.PathFor("project", "Assets"), "*.ocasset.json", SearchOption.AllDirectories));
            File.WriteAllText(revision, "{}");
            Assert.Throws<InvalidDataException>(() => project.Save(TestContext.Current.CancellationToken));
            Assert.Equal(index, File.ReadAllBytes(directory.Manifest));
        }

        Assert.Throws<InvalidDataException>(() => StudioProject.Open(directory.Project, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(99, 0)]
    [InlineData(100, 1)]
    [InlineData(349, 1)]
    [InlineData(350, 1)]
    [InlineData(long.MaxValue, 1)]
    public void OnceAnimation_ClampsToLastFrame(long ticks, int expected)
    {
        SpriteAnimation clip = MakeClip(repeat: false);
        Assert.Equal(expected, clip.SampleTicks(ticks));
    }

    [Fact]
    public void ElapsedTimeSampling_IsIndependentOfRenderFrequency()
    {
        SpriteAnimation clip = MakeClip(repeat: true);
        Assert.Equal(0, clip.Sample(TimeSpan.FromMilliseconds(99.9999)));
        Assert.Equal(1, clip.Sample(TimeSpan.FromMilliseconds(100)));
        Assert.Equal(0, clip.Sample(TimeSpan.FromMilliseconds(350)));
        Assert.Throws<ArgumentOutOfRangeException>(() => clip.SampleTicks(-1));
    }

    private static SpriteAnimation MakeClip(bool repeat) => new(Guid.NewGuid(), 1000, repeat,
        [new AnimationFrame(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100), new AnimationFrame(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 250)]);

    [Fact]
    public void RuntimeLoading_RejectsBrokenGraphEvenWhenAllDigestsMatch()
    {
        using ProjectDirectory directory = new();
        ContentBuild manifest;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            Guid texture = project.CreateTexture("Texture", 2, 2, 1, 2, 3);
            Guid atlas = project.CreateAtlas("Atlas", texture);
            project.CreateAnimation("Animation", atlas);
            project.Save(TestContext.Current.CancellationToken);
            manifest = StudioCompiler.Compile(project, directory.PathFor("build"), TestContext.Current.CancellationToken);
        }

        CompiledResource entry = manifest.Resources.Single(a => a.Kind == "atlas");
        string path = directory.PathFor("build", entry.Id.ToString("N") + ".ocatlas");
        SpriteAtlas original;
        using (FileStream input = File.OpenRead(path))
        {
            original = SpriteAssetCodec.ReadAtlas(input);
        }

        Guid missing = Guid.NewGuid();
        using (FileStream output = File.Create(path))
        {
            SpriteAssetCodec.WriteAtlas(output, new SpriteAtlas(original.Id, missing, original.Sprites));
        }

        CompiledResource changed = entry with
        {
            Sha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))),
            Dependencies = [missing],
        };
        ContentBuild wrong = manifest with
        {
            Resources = [.. manifest.Resources.Select(a => a.Id == changed.Id ? changed : a)]
        };
        File.WriteAllBytes(directory.PathFor("build", "content.json"), JsonSerializer.SerializeToUtf8Bytes(wrong, s_json));
        Assert.Contains("missing texture", Assert.Throws<InvalidDataException>(() => ContentPackage.Load(directory.PathFor("build"), TestContext.Current.CancellationToken)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TextureReplacement_PreservesDependentReferencesAndFrameIdentity()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid textureId = project.CreateTexture("Texture", 2, 1, 10, 20, 30);
        Guid atlasId = project.CreateAtlas("Atlas", textureId);
        project.CreateAnimation("Clip", atlasId);
        AnimationFrame original = project.Document.Animations[0].Frames[0];
        project.ReplaceTexture(textureId, directory.WriteTga(), TestContext.Current.CancellationToken);
        Assert.Equal(original, project.Document.Animations[0].Frames[0]);
        Assert.Equal(textureId, project.Document.Atlases[0].TextureId);
        project.Undo();
        Assert.Equal(new byte[] { 10, 20, 30, 255 }, project.Preview(textureId, TestContext.Current.CancellationToken).Pixels.Span[..4].ToArray());
        project.Redo();
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, project.Preview(textureId, TestContext.Current.CancellationToken).Pixels.Span[..4].ToArray());
    }

    [Fact]
    public void HistoryHasAFixedTransactionLimit()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        for (int i = 0; i < 75; i++)
        {
            project.Rename("Edit " + i);
        }

        int count = 0;
        while (project.CanUndo)
        {
            project.Undo();
            count++;
        }

        Assert.Equal(50, count);
        Assert.Equal("Edit 24", project.Document.Name);
    }

    [Theory]
    [InlineData("atlas")]
    [InlineData("animation")]
    public void CompiledSpriteCodecs_RejectEveryTruncationAndCorruption(string kind)
    {
        SpriteRegion sprite = new(Guid.NewGuid(), "Sprite", 0, 0, 1, 1, 0, 0);
        using MemoryStream encoded = new();
        if (kind == "atlas")
        {
            SpriteAssetCodec.WriteAtlas(encoded, new SpriteAtlas(Guid.NewGuid(), Guid.NewGuid(), [sprite]));
        }
        else
        {
            SpriteAssetCodec.WriteAnimation(encoded, MakeClip(repeat: true));
        }

        byte[] bytes = encoded.ToArray();
        void Read(byte[] data)
        {
            using MemoryStream input = new(data);
            if (kind == "atlas")
            {
                _ = SpriteAssetCodec.ReadAtlas(input);
            }
            else
            {
                _ = SpriteAssetCodec.ReadAnimation(input);
            }
        }

        Read(bytes);
        for (int length = 0; length < bytes.Length; length++)
        {
            Exception failure = Assert.ThrowsAny<Exception>(() => Read(bytes[..length]));
            Assert.True(failure is IOException or InvalidDataException, failure.ToString());
        }

        byte[] corrupt = (byte[])bytes.Clone();
        corrupt[^1] ^= 1;
        Assert.Throws<InvalidDataException>(() => Read(corrupt));
        Assert.Throws<InvalidDataException>(() => Read([.. bytes, 0]));
    }
}
