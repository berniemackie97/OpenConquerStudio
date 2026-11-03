using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio.Tests;

public sealed class ProjectWorkflowTests
{
    private static readonly JsonSerializerOptions s_json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public void DisposedSession_RejectsEditingAndCompilation()
    {
        using ProjectDirectory directory = new();
        StudioProject project = StudioProject.Create(directory.Project, "Game");
        project.Dispose();
        Assert.Throws<ObjectDisposedException>(() => project.Rename("Changed"));
        Assert.Throws<ObjectDisposedException>(() => StudioCompiler.Compile(project, directory.PathFor("build"), TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(directory.PathFor("build")));
    }
    [Fact]
    public void ImportEditSaveReopenCompile_PreservesIdentityAndAuthoredPixels()
    {
        using ProjectDirectory directory = new();
        string source = directory.WriteTga();
        byte[] originalSource = File.ReadAllBytes(source);
        Guid projectId;
        Guid assetId;
        using (StudioProject project = StudioProject.Create(directory.Project, "New game"))
        {
            projectId = project.Document.Id;
            assetId = project.ImportTexture(source, "Sprite", TestContext.Current.CancellationToken);
            Assert.True(project.IsModified);
            project.EditTexture(assetId, "Renamed sprite", flipHorizontal: true, clockwiseQuarterTurns: 1);
            TextureData preview = project.Preview(assetId, TestContext.Current.CancellationToken);
            Assert.Equal(1, preview.Width);
            Assert.Equal(2, preview.Height);
            Assert.Equal(new byte[] { 0, 255, 0, 255, 255, 0, 0, 255 }, preview.Pixels.ToArray());
            project.Rename("My game");
            project.Save(TestContext.Current.CancellationToken);
            Assert.False(project.IsModified);
        }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        Assert.Equal(projectId, reopened.Document.Id);
        Assert.Equal("My game", reopened.Document.Name);
        TextureAsset asset = Assert.Single(reopened.Document.Assets);
        Assert.Equal(assetId, asset.Id);
        Assert.Equal("Renamed sprite", asset.Name);
        Assert.True(asset.FlipHorizontal);
        ContentBuild build = StudioCompiler.Compile(reopened, directory.PathFor("build"), TestContext.Current.CancellationToken);
        Assert.Equal(assetId, Assert.Single(build.Textures).Id);
        using FileStream input = File.OpenRead(directory.PathFor("build", assetId.ToString("N") + ".octex"));
        (Guid id, TextureData compiled) = TextureCodec.Read(input);
        Assert.Equal(assetId, id);
        Assert.Equal(reopened.Preview(id, TestContext.Current.CancellationToken).Pixels.ToArray(), compiled.Pixels.ToArray());
        Assert.Equal(originalSource, File.ReadAllBytes(source));
    }

    [Fact]
    public void CreateTextureAndCompile_RequiresNoSourceInstallation()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Original game");
        Guid id = project.CreateTexture("Blue", 3, 2, 0, 0, 255);
        project.Save(TestContext.Current.CancellationToken);
        ContentBuild build = StudioCompiler.Compile(project, directory.PathFor("build"), TestContext.Current.CancellationToken);
        Assert.Equal(3, Assert.Single(build.Textures).Width);
        Assert.Equal(24, project.Preview(id, TestContext.Current.CancellationToken).Pixels.Length);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, project.Preview(id, TestContext.Current.CancellationToken).Pixels.Span[..4].ToArray());
    }

    [Fact]
    public void Compile_ProducesIdenticalBytesAcrossReopenedSessions()
    {
        using ProjectDirectory directory = new();
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            project.ImportTexture(directory.WriteTga(), "Texture", TestContext.Current.CancellationToken);
            project.CreateTexture("Color", 8, 8, 10, 20, 30);
            project.Save(TestContext.Current.CancellationToken);
            StudioCompiler.Compile(project, directory.PathFor("first"), TestContext.Current.CancellationToken);
        }

        using (StudioProject project = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken))
        {
            project.Save(TestContext.Current.CancellationToken);
            StudioCompiler.Compile(project, directory.PathFor("second"), TestContext.Current.CancellationToken);
        }

        string[] files = Directory.GetFiles(directory.PathFor("first")).Select(Path.GetFileName).Order().ToArray()!;
        Assert.Equal(files, Directory.GetFiles(directory.PathFor("second")).Select(Path.GetFileName).Order().ToArray());
        foreach (string file in files)
        {
            Assert.Equal(File.ReadAllBytes(directory.PathFor("first", file)), File.ReadAllBytes(directory.PathFor("second", file)));
        }
    }

    [Fact]
    public void Save_IsCanonicalAcrossReload()
    {
        using ProjectDirectory directory = new();
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            project.CreateTexture("First", 1, 1, 1, 2, 3);
            project.CreateTexture("Second", 1, 1, 4, 5, 6);
            project.Save(TestContext.Current.CancellationToken);
        }

        byte[] before = File.ReadAllBytes(directory.Manifest);
        using (StudioProject project = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken))
        {
            project.Save(TestContext.Current.CancellationToken);
        }

        Assert.Equal(before, File.ReadAllBytes(directory.Manifest));
    }

    [Fact]
    public void ImportDds_DecodesSupportedDxt1()
    {
        using ProjectDirectory directory = new();
        byte[] dds = new byte[136];
        foreach ((int offset, uint value) in new (int, uint)[] { (0, 0x20534444), (4, 124), (8, 0x81007), (12, 4), (16, 4), (20, 8), (76, 32), (80, 4), (84, 0x31545844), (108, 0x1000) })
        {
            BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(offset), value);
        }

        BinaryPrimitives.WriteUInt16LittleEndian(dds.AsSpan(128), 0xF800);
        string path = directory.PathFor("red.dds");
        File.WriteAllBytes(path, dds);
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid id = project.ImportTexture(path, "Red", TestContext.Current.CancellationToken);
        TextureData texture = project.Preview(id, TestContext.Current.CancellationToken);
        Assert.Equal(4, texture.Width);
        for (int i = 0; i < 16; i++)
        {
            Assert.Equal(new byte[] { 255, 0, 0, 255 }, texture.Pixels.Slice(i * 4, 4).ToArray());
        }
    }

    [Theory]
    [InlineData("bad.tga")]
    [InlineData("bad.dds")]
    [InlineData("bad.png")]
    public void FailedImport_DoesNotChangeProject(string name)
    {
        using ProjectDirectory directory = new();
        File.WriteAllBytes(directory.PathFor(name), [1, 2, 3]);
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Assert.Throws<InvalidDataException>(() => project.ImportTexture(directory.PathFor(name), "Bad", TestContext.Current.CancellationToken));
        Assert.False(project.IsModified);
        Assert.Empty(project.Document.Assets);
    }

    [Theory]
    [InlineData(65535, 65535)]
    [InlineData(4096, 4096)]
    [InlineData(0, 1)]
    public void Import_RejectsDimensionsBeforeDecoderAllocation(ushort width, ushort height)
    {
        using ProjectDirectory directory = new();
        string path = directory.WriteTga();
        byte[] bytes = File.ReadAllBytes(path);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), width);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), height);
        File.WriteAllBytes(path, bytes);
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Assert.Throws<InvalidDataException>(() => project.ImportTexture(path, "Oversize", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("version")]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("null")]
    [InlineData("truncated")]
    public void Open_RejectsUnsupportedOrMalformedProject(string corruption)
    {
        using ProjectDirectory directory = new();
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
        }
        string json = File.ReadAllText(directory.Manifest);
        json = corruption switch
        {
            "version" => json.Replace("\"schemaVersion\": 2", "\"schemaVersion\": 99", StringComparison.Ordinal),
            "duplicate" => json.Replace("\"schemaVersion\": 2", "\"schemaVersion\": 2, \"schemaVersion\": 2", StringComparison.Ordinal),
            "unknown" => json.Replace("\"schemaVersion\": 2", "\"schemaVersion\": 2, \"surprise\": 0", StringComparison.Ordinal),
            "null" => "null",
            _ => "{",
        };
        File.WriteAllText(directory.Manifest, json);
        Exception failure = Assert.ThrowsAny<Exception>(() => StudioProject.Open(directory.Project, TestContext.Current.CancellationToken));
        Assert.True(failure is InvalidDataException or JsonException, failure.ToString());
        // A failed open must release its editing lease.
        File.WriteAllText(directory.Manifest, "{}");
        Assert.ThrowsAny<JsonException>(() => StudioProject.Open(directory.Project, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Open_RejectsCorruptedPixels()
    {
        using ProjectDirectory directory = new();
        directory.CreateSavedTexture();
        string path = Assert.Single(Directory.GetFiles(directory.PathFor("project", "Inputs")));
        File.WriteAllBytes(path, [0, 0, 0, 0]);
        Assert.Throws<InvalidDataException>(() => StudioProject.Open(directory.Project, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Save_RejectsOutsideChangesWithoutReplacingManifest()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        File.WriteAllText(directory.Manifest, "outside edit");
        project.Rename("New name");
        Assert.Throws<IOException>(() => project.Save(TestContext.Current.CancellationToken));
        Assert.True(project.IsModified);
        Assert.Equal("outside edit", File.ReadAllText(directory.Manifest));
    }

    [Fact]
    public void EditingLease_RejectsSecondSessionAndReleasesOnDispose()
    {
        using ProjectDirectory directory = new();
        using (StudioProject first = StudioProject.Create(directory.Project, "Game"))
        {
            Assert.Throws<IOException>(() => StudioProject.Open(directory.Project, TestContext.Current.CancellationToken));
        }

        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        Assert.Equal("Game", reopened.Document.Name);
    }

    [Fact]
    public void SaveFailure_PreservesOldManifestAndModifiedState()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        byte[] before = File.ReadAllBytes(directory.Manifest);
        Guid id = project.CreateTexture("New", 1, 1, 1, 2, 3);
        TextureAsset asset = project.Document.Assets.Single(a => a.Id == id);
        Directory.CreateDirectory(directory.PathFor("project", "Inputs", asset.PixelDigest + ".rgba"));
        Assert.Throws<IOException>(() => project.Save(TestContext.Current.CancellationToken));
        Assert.True(project.IsModified);
        Assert.Equal(before, File.ReadAllBytes(directory.Manifest));
    }

    [Fact]
    public void CanceledSaveAndBuild_PreserveProjectAndPublishNothing()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        project.CreateTexture("Color", 2, 2, 1, 2, 3);
        byte[] before = File.ReadAllBytes(directory.Manifest);
        using CancellationTokenSource canceled = new();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => project.Save(canceled.Token));
        Assert.Equal(before, File.ReadAllBytes(directory.Manifest));
        Assert.True(project.IsModified);
        project.Save(TestContext.Current.CancellationToken);
        Assert.Throws<OperationCanceledException>(() => StudioCompiler.Compile(project, directory.PathFor("build"), canceled.Token));
        Assert.False(Directory.Exists(directory.PathFor("build")));
        Assert.Empty(Directory.GetDirectories(directory.Root, ".studio-build-*"));
    }

    [Fact]
    public void Compile_RejectsUnsavedEditsAndExistingDestination()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        project.CreateTexture("Color", 2, 2, 1, 2, 3);
        Assert.Throws<InvalidOperationException>(() => StudioCompiler.Compile(project, directory.PathFor("build"), TestContext.Current.CancellationToken));
        project.Save(TestContext.Current.CancellationToken);
        StudioCompiler.Compile(project, directory.PathFor("build"), TestContext.Current.CancellationToken);
        Assert.Throws<IOException>(() => StudioCompiler.Compile(project, directory.PathFor("build"), TestContext.Current.CancellationToken));
        Assert.Throws<IOException>(() => StudioCompiler.Compile(project, directory.PathFor("project", "Inputs", "build"), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("pixels")]
    [InlineData("trailing")]
    [InlineData("extra")]
    [InlineData("missing")]
    [InlineData("identity")]
    public void ValidateBuild_RejectsTamperingAndIncompleteCoverage(string corruption)
    {
        using ProjectDirectory directory = new();
        directory.CreateSavedTexture();
        string buildPath = directory.PathFor("build");
        ContentBuild manifest;
        using (StudioProject project = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken))
        {
            manifest = StudioCompiler.Compile(project, buildPath, TestContext.Current.CancellationToken);
        }

        string texturePath = Assert.Single(Directory.GetFiles(buildPath, "*.octex"));
        switch (corruption)
        {
            case "extra":
                File.WriteAllText(Path.Combine(buildPath, "extra.txt"), "extra");
                break;
            case "missing":
                File.Delete(texturePath);
                break;
            case "pixels":
                byte[] bytes = File.ReadAllBytes(texturePath);
                bytes[^1] ^= 1;
                File.WriteAllBytes(texturePath, bytes);
                break;
            case "identity":
                CompiledTexture entry = manifest.Textures[0] with
                {
                    Width = 2
                };
                ContentBuild wrong = manifest with
                {
                    Textures = [entry]
                };
                File.WriteAllText(Path.Combine(buildPath, "content.json"), JsonSerializer.Serialize(wrong, s_json));
                break;
            default:
                using (FileStream stream = new(texturePath, FileMode.Append))
                {
                    stream.WriteByte(0);
                }
                break;
        }

        Exception failure = Assert.ThrowsAny<Exception>(() => StudioCompiler.Validate(buildPath, TestContext.Current.CancellationToken));
        Assert.True(failure is IOException or InvalidDataException, failure.ToString());
    }

    [Fact]
    public void LinkedInputAndProjectPaths_AreRejected()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows link creation requires a separately provisioned privilege.");
        }

        using ProjectDirectory directory = new();
        directory.CreateSavedTexture();
        string link = directory.PathFor("linked-project");
        Directory.CreateSymbolicLink(link, directory.Project);
        Assert.Throws<IOException>(() => StudioProject.Open(link, TestContext.Current.CancellationToken));
        Directory.Delete(link);
        string inputs = directory.PathFor("project", "Inputs");
        Directory.Move(inputs, directory.PathFor("outside"));
        Directory.CreateSymbolicLink(inputs, directory.PathFor("outside"));
        Assert.Throws<IOException>(() => StudioProject.Open(directory.Project, TestContext.Current.CancellationToken));
        Directory.Delete(inputs);
        Directory.Move(directory.PathFor("outside"), inputs);
    }

    [Fact]
    public void AssetValidation_RejectsDuplicateIdentitiesAndUnsafeDigestPaths()
    {
        TextureAsset asset = new(Guid.NewGuid(), "Image", 1, 1, new string('a', 64), "created", new string('b', 64), false, 0);
        ProjectDocument valid = new(ProjectDocument.CurrentVersion, Guid.NewGuid(), "Game", [asset], [], []);
        valid.Validate();
        Assert.Throws<InvalidDataException>(() => (valid with { Assets = [asset, asset] }).Validate());
        Assert.Throws<InvalidDataException>(() => (valid with { Assets = [asset with { PixelDigest = "../../outside" }] }).Validate());
        Assert.Throws<InvalidDataException>(() => (valid with { Assets = [asset with { ClockwiseQuarterTurns = 4 }] }).Validate());
        Assert.Throws<InvalidDataException>(() => (valid with { Assets = default }).Validate());
        ImmutableArray<TextureAsset> oversized = Enumerable.Range(0, 5).Select(_ => asset with { Id = Guid.NewGuid(), Width = 2048, Height = 2048 }).ToImmutableArray();
        Assert.Throws<InvalidDataException>(() => (valid with { Assets = oversized }).Validate());
    }
}
