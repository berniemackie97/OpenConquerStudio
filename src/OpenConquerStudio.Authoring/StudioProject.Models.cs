using OpenConquer.Content.Mesh;

namespace OpenConquerStudio.Authoring;

public sealed partial class StudioProject
{
    public Guid ImportModel(string name, C3ModelData model)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(model);
        if (Document.Resources.Any(r => r.Id == model.Definition.Id))
        {
            throw new InvalidDataException("The model identity is already in the project.");
        }
        byte[] data = C3ModelCodec.Write(model);
        ModelAsset asset = new(model.Definition.Id, name, ProjectFiles.Hash(data), data.Length);
        ProjectDocument updated = Document with
        {
            Models = [.. Document.Models, asset]
        };
        updated.Validate();
        _models.TryAdd(asset.DataDigest, data);
        Apply(updated);
        return asset.Id;
    }

    public C3ModelData PreviewModel(Guid id) => C3ModelCodec.Read(ModelBytes(id));

    internal ReadOnlyMemory<byte> ModelBytes(Guid id)
    {
        ThrowIfDisposed();
        ModelAsset asset = Document.Models.FirstOrDefault(a => a.Id == id) ?? throw new InvalidDataException("Model does not exist.");
        return _models[asset.DataDigest];
    }

    public void EditModel(Guid id, string name, int clipIndex, int frameMilliseconds, bool repeat)
    {
        C3ModelData model = PreviewModel(id);
        if (clipIndex >= 0)
        {
            model = model.WithPlayback(clipIndex, frameMilliseconds, repeat);
        }
        ReplaceModel(id, name, model);
    }

    public void AddModelMotion(Guid id, string name, string path, CancellationToken token = default)
    {
        C3ModelData model = PreviewModel(id);
        byte[] bytes = ProjectFiles.Read(path, C3MeshFile.MaximumFileBytes);
        token.ThrowIfCancellationRequested();
        string sourcePath = "motions/" + ProjectFiles.Hash(bytes) + ".c3";
        Dictionary<string, byte[]> files = model.Definition.Sources.ToDictionary(f => f.Path, f => model.Source(f.Path).ToArray(), StringComparer.Ordinal);
        files[sourcePath] = bytes;
        C3ModelData replacement = C3ModelData.Create(id, model.Definition.Parts,
            [.. model.Definition.Clips, new C3ModelClip(name, sourcePath, 66, true)], model.Definition.VirtualMeshNames, files);
        for (int part = 0; part < model.Definition.Parts.Length; part++)
        {
            _ = replacement.DecodePart(part, replacement.Definition.Clips.Length - 1);
        }
        ReplaceModel(id, Document.Models.Single(a => a.Id == id).Name, replacement);
    }

    private void ReplaceModel(Guid id, string name, C3ModelData model)
    {
        byte[] data = C3ModelCodec.Write(model);
        ModelAsset asset = new(id, name, ProjectFiles.Hash(data), data.Length);
        ProjectDocument updated = Document with
        {
            Models = [.. Document.Models.Select(a => a.Id == id ? asset : a)]
        };
        updated.Validate();
        if (Document.Models.Single(a => a.Id == id) != asset)
        {
            _models.TryAdd(asset.DataDigest, data);
            Apply(updated);
        }
    }
}
