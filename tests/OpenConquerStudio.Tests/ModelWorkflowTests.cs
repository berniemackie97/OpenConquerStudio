using System.Numerics;
using OpenConquer.Content.Mesh;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.ConquerImport;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio.Tests;

public sealed class ModelWorkflowTests
{
    [Fact]
    public void EditUndoSaveReopenAndBuildPreserveNativeSourcesAndBindings()
    {
        using ProjectDirectory directory = new();
        C3ModelData original = C3ModelFixture.Create();
        Guid id;
        using (StudioProject project = StudioProject.Create(directory.Project, "Characters"))
        {
            id = project.ImportModel("Actor", original);
            project.EditModel(id, "Edited actor", 0, 42, false);
            project.Undo();
            Assert.Equal(66, project.PreviewModel(id).Definition.Clips[0].FrameMilliseconds);
            project.Redo();
            project.Save(TestContext.Current.CancellationToken);
        }
        using StudioProject reopened = StudioProject.Open(directory.Project, TestContext.Current.CancellationToken);
        C3ModelData restored = reopened.PreviewModel(id);
        Assert.Equal("Edited actor", Assert.Single(reopened.Document.Models).Name);
        Assert.Equal(42, restored.Definition.Clips[0].FrameMilliseconds);
        Assert.False(restored.Definition.Clips[0].Repeat);
        foreach (C3ModelSource source in original.Definition.Sources)
        {
            Assert.Equal(original.Source(source.Path).ToArray(), restored.Source(source.Path).ToArray());
        }
        Assert.Equal(original.Definition.Bindings.Select(b => (b.PartIndex, b.ParentPartIndex, b.ParentSubmesh)),
            restored.Definition.Bindings.Select(b => (b.PartIndex, b.ParentPartIndex, b.ParentSubmesh)));
        foreach (C3ModelPartBinding binding in original.Definition.Bindings)
        {
            Assert.Equal(binding.LocalMatrix.ToArray(), restored.Definition.Bindings.Single(b => b.PartIndex == binding.PartIndex).LocalMatrix.ToArray());
        }
        StudioCompiler.Compile(reopened, directory.PathFor("first"), TestContext.Current.CancellationToken);
        StudioCompiler.Compile(reopened, directory.PathFor("second"), TestContext.Current.CancellationToken);
        foreach (string first in Directory.EnumerateFiles(directory.PathFor("first")))
        {
            Assert.Equal(System.IO.File.ReadAllBytes(first), System.IO.File.ReadAllBytes(directory.PathFor("second", Path.GetFileName(first))));
        }
        ContentPackage package = ContentPackage.Load(directory.PathFor("first"), TestContext.Current.CancellationToken);
        Assert.Equal(original.Source("c3/body.c3").ToArray(), package.Models[id].Source("c3/body.c3").ToArray());
        C3ModelPreview preview = C3ModelPreview.Create(package.Models[id], 0);
        Assert.Equal(6u, preview.FrameCount);
        Assert.Equal(61u, preview.Parts[1].Tracks[0].Motion!.FrameCount);
        Matrix4x4 expectedBone = Matrix4x4.CreateTranslation(14, 22, 36);
        expectedBone.M12 = 0.2f;
        Assert.Equal(Matrix4x4.CreateScale(2, 3, 4) * expectedBone, preview.PartTransform(1, 2));
    }

    [Fact]
    public void AddingMotionKeepsPartOverridesAndBadImportsLeaveProjectUnchanged()
    {
        using ProjectDirectory directory = new();
        using StudioProject project = StudioProject.Create(directory.Project, "Game");
        Guid id = project.ImportModel("Actor", C3ModelFixture.Create());
        string motion = directory.PathFor("walk.c3");
        System.IO.File.WriteAllBytes(motion, C3ModelFixture.File("body_surface", "v_armet"));
        project.AddModelMotion(id, "Walk", motion, TestContext.Current.CancellationToken);
        C3ModelData model = project.PreviewModel(id);
        Assert.Equal(2, model.Definition.Clips.Length);
        Assert.Equal("c3/hair.c3", Assert.Single(model.Definition.Clips[1].PartMotions).MotionPath);
        Assert.Equal(2, model.Definition.Bindings.Length);
        ProjectDocument before = project.Document;
        System.IO.File.WriteAllBytes(motion, [1, 2, 3]);
        Assert.Throws<InvalidDataException>(() => project.AddModelMotion(id, "Broken", motion, TestContext.Current.CancellationToken));
        Assert.Same(before, project.Document);
    }

    [Fact]
    public void CyclesMissingAnchorsAndTamperedPayloadsFail()
    {
        C3ModelData model = C3ModelFixture.Create();
        Dictionary<string, byte[]> files = model.Definition.Sources.ToDictionary(f => f.Path, f => model.Source(f.Path).ToArray());
        Assert.Throws<InvalidDataException>(() => C3ModelData.Create(Guid.NewGuid(), model.Definition.Parts, model.Definition.Clips,
            model.Definition.VirtualMeshNames, files, [new C3ModelPartBinding(1, 1, "v_armet", [])]));
        Assert.Throws<InvalidDataException>(() => C3ModelData.Create(Guid.NewGuid(), model.Definition.Parts, model.Definition.Clips,
            model.Definition.VirtualMeshNames, files, [new C3ModelPartBinding(1, 0, "missing", [])]));
        byte[] bytes = C3ModelCodec.Write(model);
        bytes[^1] ^= 1;
        Assert.Throws<InvalidDataException>(() => C3ModelCodec.Read(bytes));
    }

    [Fact]
    public void VersionOneBodyResourcesReadWithoutLosingSourceBytes()
    {
        C3ModelData fixture = C3ModelFixture.Create();
        C3ModelData single = C3ModelData.Create(Guid.NewGuid(), [fixture.Definition.Parts[0]],
            [new C3ModelClip("Idle", "c3/body.c3", 66, true)], ["v_armet"],
            new Dictionary<string, byte[]> { ["c3/body.c3"] = fixture.Source("c3/body.c3").ToArray() });
        byte[] current = C3ModelCodec.Write(single);
        int jsonLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(current.AsSpan(8));
        var json = System.Text.Json.Nodes.JsonNode.Parse(current.AsSpan(48, jsonLength))!.AsObject();
        json["schemaVersion"] = 1;
        json.Remove("bindings");
        json.Remove("characterSource");
        foreach (var clip in json["clips"]!.AsArray())
        {
            clip!.AsObject().Remove("partMotions");
        }
        byte[] metadata = System.Text.Encoding.UTF8.GetBytes(json.ToJsonString());
        byte[] payload = current[(48 + jsonLength)..];
        byte[] legacy = new byte[48 + metadata.Length + payload.Length];
        current.AsSpan(0, 48).CopyTo(legacy);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(legacy.AsSpan(4), 1);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(legacy.AsSpan(8), metadata.Length);
        metadata.CopyTo(legacy.AsSpan(48));
        payload.CopyTo(legacy.AsSpan(48 + metadata.Length));
        System.Security.Cryptography.SHA256.HashData(legacy.AsSpan(48), legacy.AsSpan(16, 32));
        C3ModelData restored = C3ModelCodec.Read(legacy);
        Assert.Equal(1, restored.Definition.SchemaVersion);
        Assert.Equal(single.Source("c3/body.c3").ToArray(), restored.Source("c3/body.c3").ToArray());
        Assert.Empty(restored.Definition.Bindings);
        Assert.Equal(2, C3ModelCodec.Read(C3ModelCodec.Write(restored)).Definition.SchemaVersion);
    }

    [Fact]
    public void EmptyBoneMotionCannotAllocateUnboundedFrameIndexes()
    {
        using MemoryStream bytes = new();
        bytes.Write(new byte[16]);
        using (BinaryWriter writer = new(bytes, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("MOTI"u8);
            writer.Write(12);
            writer.Write(0u);
            writer.Write(1_000_001u);
            writer.Write(0u);
        }
        C3ResolvedPart part = new("body", "c3/huge.c3", "", "", 0, 0, 0, 0);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => C3ModelData.Create(Guid.NewGuid(), [part], [], [],
            new Dictionary<string, byte[]> { ["c3/huge.c3"] = bytes.ToArray() }));
        Assert.Contains("allocation budget", error.Message);
    }

    [Fact]
    public void RealBodyAndHairRoundTripUsesCurrentClientCatalog()
    {
        string client = ProjectDirectory.RepositoryRoot;
        string content = Path.Combine(client, "content", "retail-5517", "payload");
        Assert.SkipUnless(System.IO.File.Exists(Path.Combine(content, "ini", "c3.wdb")), "Local C3 corpus is unavailable.");
        var source = OpenConquer.Content.PackagedClientContentSource.Open(content);
        C3ModelData model = CharacterImporter.Read(source, 2, 335, TestContext.Current.CancellationToken);
        C3ModelData restored = C3ModelCodec.Read(C3ModelCodec.Write(model));
        Assert.Equal(16u, C3ModelPreview.Create(restored, 0).FrameCount);
        Assert.Equal(2, restored.Definition.Parts.Length);
        Assert.Equal(new C3CharacterSource(2, 335, 2_000_000, 2_119_335), restored.Definition.CharacterSource);
        Assert.Equal("v_armet", Assert.Single(restored.Definition.Bindings).ParentSubmesh);
        foreach (C3ModelSource file in model.Definition.Sources)
        {
            Assert.Equal(model.Source(file.Path).ToArray(), restored.Source(file.Path).ToArray());
        }
        _ = C3ModelPreview.Create(restored, 0).PartTransform(1, 3);
        C3ModelData equipped = CharacterImporter.Read(source, 2, 0, 2_130_000, 2_111_000, TestContext.Current.CancellationToken);
        C3ModelData equipmentRestored = C3ModelCodec.Read(C3ModelCodec.Write(equipped));
        Assert.Equal(new C3CharacterSource(2, 0, 2_130_000, 2_111_000), equipmentRestored.Definition.CharacterSource);
        _ = C3ModelPreview.Create(equipmentRestored, 0).PartTransform(1, 4);
    }
}
