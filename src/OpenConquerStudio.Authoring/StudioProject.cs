using System.Collections.Immutable;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio.Authoring;

/// <summary>A single-owner editing session. Save publishes immutable revisions before replacing the project index.</summary>
public sealed partial class StudioProject : IDisposable
{
    private readonly FileStream _lease;
    private readonly Dictionary<string, TextureData> _pixels;
    private readonly Dictionary<string, byte[]> _models;
    private readonly Dictionary<string, byte[]> _terrainCells;
    private readonly Dictionary<string, byte[]> _sourceInputs;
    private readonly List<ProjectDocument> _undo = [];
    private readonly List<ProjectDocument> _redo = [];
    private ProjectDocument? _savedDocument;
    private string? _savedDigest;
    private bool _disposed;

    private StudioProject(string root, FileStream lease, ProjectDocument document, Dictionary<string, TextureData> pixels, string? savedDigest, Dictionary<string, byte[]>? models = null, Dictionary<string, byte[]>? terrainCells = null,
        Dictionary<string, byte[]>? sourceInputs = null)
    {
        Root = root;
        _lease = lease;
        Document = document;
        _pixels = pixels;
        _models = models ?? [];
        _terrainCells = terrainCells ?? [];
        _sourceInputs = sourceInputs ?? [];
        _savedDocument = savedDigest is null ? null : document;
        _savedDigest = savedDigest;
        IsModified = savedDigest is null;
    }

    public string Root
    {
        get;
    }
    public ProjectDocument Document
    {
        get; private set;
    }
    public bool IsModified
    {
        get; private set;
    }

    public static StudioProject Create(string root, string name)
    {
        ProjectDocument document = new(ProjectDocument.CurrentVersion, Guid.NewGuid(), name, [], [], []);
        document.Validate();
        root = ProjectFiles.RequireSafePath(root);
        Directory.CreateDirectory(root);
        if (Directory.EnumerateFileSystemEntries(root).Any())
        {
            throw new IOException("Choose an empty directory for the new project.");
        }

        FileStream lease = AcquireLease(root);
        StudioProject project = new(root, lease, document, [], null);
        try
        {
            project.Save();
            return project;
        }
        catch
        {
            project.Dispose();
            throw;
        }
    }

    public static StudioProject Open(string root, CancellationToken cancellationToken = default)
    {
        root = ProjectFiles.RequireSafePath(root);
        FileStream lease = AcquireLease(root);
        try
        {
            (ProjectDocument document, Dictionary<string, TextureData> pixels, string digest, Dictionary<string, byte[]> models,
                Dictionary<string, byte[]> terrains, Dictionary<string, byte[]> sourceInputs) =
                ProjectStorage.Read(root, cancellationToken);
            return new StudioProject(root, lease, document, pixels, digest, models, terrains, sourceInputs);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private static FileStream AcquireLease(string root)
    {
        string path = ProjectFiles.RequireSafePath(Path.Combine(root, ".studio.lock"));
        return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    public Guid ImportTexture(string path, string name, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        (TextureData texture, string format, string digest) = TextureImporter.Read(path, cancellationToken);
        return AddTexture(name, texture, format, digest);
    }

    public Guid ImportImage(string name, TextureData texture, string sourceKey)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(texture);
        return AddTexture(name, texture, "rgba", ProjectFiles.Hash(texture.Pixels.Span), sourceKey);
    }

    public Guid CreateTexture(string name, int width, int height, byte red, byte green, byte blue, byte alpha = 255)
    {
        ThrowIfDisposed();
        byte[] pixels = new byte[TextureData.GetByteLength(width, height)];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = red;
            pixels[i + 1] = green;
            pixels[i + 2] = blue;
            pixels[i + 3] = alpha;
        }

        return AddTexture(name, new TextureData(width, height, pixels), "created", ProjectFiles.Hash(pixels));
    }

    private Guid AddTexture(string name, TextureData texture, string format, string sourceDigest, string? sourceKey = null)
    {
        TextureAsset asset = new(Guid.NewGuid(), name, texture.Width, texture.Height, ProjectFiles.Hash(texture.Pixels.Span), format, sourceDigest, false, 0, sourceKey);
        ProjectDocument updated = Document with
        {
            Assets = [.. Document.Assets, asset]
        };
        updated.Validate();
        _pixels.TryAdd(asset.PixelDigest, texture);
        Apply(updated);
        return asset.Id;
    }

    public void EditTexture(Guid id, string name, bool flipHorizontal, int clockwiseQuarterTurns)
    {
        ThrowIfDisposed();
        TextureAsset original = FindAsset(id);
        TextureAsset updated = original with
        {
            Name = name,
            FlipHorizontal = flipHorizontal,
            ClockwiseQuarterTurns = clockwiseQuarterTurns
        };
        ProjectDocument document = Document with
        {
            Assets = Document.Assets.Select(a => a.Id == id ? updated : a).ToImmutableArray()
        };
        document.Validate();
        if (original != updated)
        {
            Apply(document);
        }
    }

    public void ReplaceTexture(Guid id, string path, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        TextureAsset original = FindAsset(id);
        (TextureData texture, string format, string digest) = TextureImporter.Read(path, cancellationToken);
        ReplaceTexture(original, texture, format, digest, original.SourceKey);
    }

    private void ReplaceTexture(TextureAsset original, TextureData texture, string format, string sourceDigest, string? sourceKey)
    {
        TextureAsset updated = ReplacedTexture(original, texture, format, sourceDigest, sourceKey);
        if (updated == original)
        {
            return;
        }

        ProjectDocument document = Document with
        {
            Assets = Document.Assets.Select(a => a.Id == original.Id ? updated : a).ToImmutableArray()
        };
        document.Validate();
        _pixels.TryAdd(updated.PixelDigest, texture);
        Apply(document);
    }

    private static TextureAsset ReplacedTexture(TextureAsset asset, TextureData texture, string format, string sourceDigest, string? sourceKey) =>
        asset with
        {
            Width = texture.Width,
            Height = texture.Height,
            PixelDigest = ProjectFiles.Hash(texture.Pixels.Span),
            SourceFormat = format,
            SourceDigest = sourceDigest,
            SourceKey = sourceKey,
        };

    public TextureData Preview(Guid id, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        TextureAsset asset = FindAsset(id);
        TextureData pixels = _pixels[asset.PixelDigest];
        TextureData texture = pixels.Width == asset.Width && pixels.Height == asset.Height
            ? pixels : new TextureData(asset.Width, asset.Height, pixels.Pixels.Span);
        return texture.Transform(asset.FlipHorizontal, asset.ClockwiseQuarterTurns, cancellationToken);
    }

    public void Rename(string name)
    {
        ThrowIfDisposed();
        ProjectDocument updated = Document with
        {
            Name = name
        };
        updated.Validate();
        if (name != Document.Name)
        {
            Apply(updated);
        }
    }

    public void Save(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _savedDigest = ProjectStorage.Save(Root, Document, _pixels, _models, _terrainCells, _sourceInputs, _savedDigest, cancellationToken);
        _savedDocument = Document;
        IsModified = false;
    }

    public bool CanUndo => !_disposed && _undo.Count != 0;
    public bool CanRedo => !_disposed && _redo.Count != 0;

    public void Undo() => MoveHistory(_undo, _redo);
    public void Redo() => MoveHistory(_redo, _undo);

    private void MoveHistory(List<ProjectDocument> from, List<ProjectDocument> to)
    {
        ThrowIfDisposed();
        if (from.Count == 0)
        {
            return;
        }

        to.Add(Document);
        Document = from[^1];
        from.RemoveAt(from.Count - 1);
        IsModified = Document != _savedDocument;
    }

    private void Apply(ProjectDocument document)
    {
        document.Validate();
        _undo.Add(Document);
        _redo.Clear();
        Document = document;
        IsModified = Document != _savedDocument;
        TrimHistory();
    }

    private void TrimHistory()
    {
        while (_undo.Count > 50)
        {
            _undo.RemoveAt(0);
        }

        HashSet<string> retained;
        HashSet<string> retainedModels;
        HashSet<string> retainedTerrain;
        HashSet<string> retainedSourceInputs;
        while (true)
        {
            retained = Document.Assets.Concat(_undo.SelectMany(d => d.Assets)).Select(a => a.PixelDigest).ToHashSet(StringComparer.Ordinal);
            retainedModels = Document.Models.Concat(_undo.SelectMany(d => d.Models)).Select(a => a.DataDigest).ToHashSet(StringComparer.Ordinal);
            retainedTerrain = Document.Terrains.Concat(_undo.SelectMany(d => d.Terrains)).Select(a => a.CellDigest).ToHashSet(StringComparer.Ordinal);
            retainedSourceInputs = Document.Terrains.Concat(_undo.SelectMany(d => d.Terrains))
                .Select(a => a.Source?.SnapshotDigest).OfType<string>().ToHashSet(StringComparer.Ordinal);
            if (_undo.Count == 0 || retained.Sum(d => (long)_pixels[d].Pixels.Length) +
                retainedModels.Sum(d => (long)_models[d].Length) + retainedTerrain.Sum(d => (long)_terrainCells[d].Length) +
                retainedSourceInputs.Sum(d => (long)_sourceInputs[d].Length) <= 128 * 1024 * 1024)
            {
                break;
            }

            _undo.RemoveAt(0);
        }

        foreach (string digest in _sourceInputs.Keys.Where(d => !retainedSourceInputs.Contains(d)).ToArray())
        {
            _sourceInputs.Remove(digest);
        }

        foreach (string digest in _terrainCells.Keys.Where(d => !retainedTerrain.Contains(d)).ToArray())
        {
            _terrainCells.Remove(digest);
        }

        foreach (string digest in _models.Keys.Where(d => !retainedModels.Contains(d)).ToArray())
        {
            _models.Remove(digest);
        }

        foreach (string digest in _pixels.Keys.Where(d => !retained.Contains(d)).ToArray())
        {
            _pixels.Remove(digest);
        }
    }

    public static StudioProject Migrate(string source, string destination, CancellationToken token = default)
    {
        source = ProjectFiles.RequireSafePath(source);
        using FileStream sourceLease = AcquireLease(source);
        byte[] original = ProjectFiles.Read(Path.Combine(source, ProjectStorage.ManifestName), ProjectFiles.MaximumManifestBytes);
        LegacyProject legacy = ProjectFiles.Deserialize<LegacyProject>(original);
        if (legacy.SchemaVersion != 1)
        {
            throw new InvalidDataException("Only version 1 projects can be migrated. Version 2 projects open directly.");
        }

        ProjectDocument document = new(ProjectDocument.CurrentVersion, legacy.Id, legacy.Name, legacy.Assets, [], []);
        document.Validate();
        Dictionary<string, TextureData> pixels = ProjectStorage.ReadPixels(source, document, token);
        destination = ProjectFiles.RequireSafePath(destination);
        if (!Directory.Exists(destination) || Directory.EnumerateFileSystemEntries(destination).Any())
        {
            throw new IOException("Choose an existing empty directory for the migrated project.");
        }

        token.ThrowIfCancellationRequested();
        if (!ProjectFiles.Read(Path.Combine(source, ProjectStorage.ManifestName), ProjectFiles.MaximumManifestBytes).AsSpan().SequenceEqual(original))
        {
            throw new IOException("The source project changed during migration.");
        }

        StudioProject target = new(destination, AcquireLease(destination), document, pixels, null);
        try
        {
            target.Save(token);
            return target;
        }
        catch
        {
            target.Dispose();
            throw;
        }
    }

    internal TextureAsset FindAsset(Guid id) => Document.Assets.FirstOrDefault(a => a.Id == id) ?? throw new InvalidDataException($"Asset '{id}' does not exist.");

    internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (!_disposed)
        {
            _lease.Dispose();
            _pixels.Clear();
            _models.Clear();
            _terrainCells.Clear();
            _sourceInputs.Clear();
            _undo.Clear();
            _redo.Clear();
            _disposed = true;
        }
    }
}
