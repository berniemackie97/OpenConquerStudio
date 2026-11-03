using System.Collections.Immutable;
using OpenConquer.Content.Mesh;

namespace OpenConquerStudio.Runtime;

public sealed record CompiledTexture(Guid Id, string Name, int Width, int Height, long Length, string Sha256);
public sealed record CompiledResource(Guid Id, string Name, string Kind, long Length, string Sha256, ImmutableArray<Guid> Dependencies);
public sealed record ContentBuild(int SchemaVersion, Guid ProjectId, ImmutableArray<CompiledTexture> Textures, ImmutableArray<CompiledResource> Resources);

/// <summary>Loads a complete, bounded content build and resolves its runtime resource graph.</summary>
public sealed class ContentPackage
{
    public const string ManifestName = "content.json";
    public const int MaximumTextures = 64;
    public const int MaximumResources = 256;
    public const int MaximumTotalPixelBytes = 64 * 1024 * 1024;

    private ContentPackage(ContentBuild manifest, SpriteLibrary sprites, Dictionary<Guid, C3ModelData> models)
    {
        Manifest = manifest;
        Sprites = sprites;
        Models = models.AsReadOnly();
    }

    public ContentBuild Manifest
    {
        get;
    }
    public IReadOnlyDictionary<Guid, C3ModelData> Models
    {
        get;
    }

    public SpriteLibrary Sprites
    {
        get;
    }

    public static ContentPackage Load(string directory, CancellationToken cancellationToken = default)
    {
        directory = AssetFileIO.RequireSafePath(directory);
        ContentBuild manifest = AssetFileIO.Deserialize<ContentBuild>(AssetFileIO.Read(Path.Combine(directory, ManifestName), AssetFileIO.MaximumManifestBytes));
        if (manifest.SchemaVersion != 2 || manifest.ProjectId == Guid.Empty || manifest.Textures.IsDefault || manifest.Resources.IsDefault ||
            manifest.Textures.Length > MaximumTextures || manifest.Textures.Length + manifest.Resources.Length > MaximumResources)
        {
            throw new InvalidDataException("The compiled content manifest has an unsupported version or invalid identity/count.");
        }

        long total = 0;
        Guid previous = Guid.Empty;
        HashSet<string> expected = new(StringComparer.Ordinal) { ManifestName };
        Dictionary<Guid, TextureData> textures = [];
        foreach (CompiledTexture entry in manifest.Textures)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry is null || entry.Id == Guid.Empty || entry.Id.CompareTo(previous) <= 0)
            {
                throw new InvalidDataException("Compiled texture IDs must be unique and sorted.");
            }

            previous = entry.Id;
            AssetFileIO.ValidateName(entry.Name);
            AssetFileIO.ValidateDigest(entry.Sha256);
            total += TextureData.GetByteLength(entry.Width, entry.Height);
            if (total > MaximumTotalPixelBytes)
            {
                throw new InvalidDataException("Compiled pixels exceed the project budget.");
            }

            string fileName = FileName(entry.Id);
            expected.Add(fileName);
            byte[] bytes = AssetFileIO.Read(Path.Combine(directory, fileName), TextureData.MaximumPixelBytes + 68);
            if (bytes.Length != entry.Length || AssetFileIO.Hash(bytes) != entry.Sha256)
            {
                throw new InvalidDataException($"Compiled texture '{entry.Name}' does not match its content manifest.");
            }

            using MemoryStream input = new(bytes, writable: false);
            (Guid id, TextureData texture) = TextureCodec.Read(input);
            if (id != entry.Id || texture.Width != entry.Width || texture.Height != entry.Height)
            {
                throw new InvalidDataException($"Compiled texture '{entry.Name}' has inconsistent metadata.");
            }

            textures.Add(id, texture);
        }

        Dictionary<Guid, C3ModelData> models = [];
        long modelBytes = 0;
        List<SpriteAtlas> atlases = [];
        List<SpriteAnimation> animations = [];
        previous = Guid.Empty;
        long metadata = 0;
        foreach (CompiledResource entry in manifest.Resources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry is null || entry.Id == Guid.Empty || entry.Id.CompareTo(previous) <= 0 || textures.ContainsKey(entry.Id) || entry.Dependencies.IsDefault)
            {
                throw new InvalidDataException("Compiled resource identities must be unique and sorted, with explicit dependencies.");
            }

            previous = entry.Id;
            AssetFileIO.ValidateName(entry.Name);
            AssetFileIO.ValidateDigest(entry.Sha256);
            string name = ResourceFileName(entry.Id, entry.Kind);
            expected.Add(name);
            byte[] bytes = AssetFileIO.Read(Path.Combine(directory, name), entry.Kind == "model" ? C3ModelCodec.MaximumLength : SpriteAssetCodec.MaximumLength);
            if (entry.Kind == "model")
            {
                modelBytes += bytes.Length;
            }
            else
            {
                metadata += bytes.Length;
            }
            if (bytes.Length != entry.Length || metadata > 8 * 1024 * 1024 || modelBytes > 128 * 1024 * 1024 || AssetFileIO.Hash(bytes) != entry.Sha256)
            {
                throw new InvalidDataException("Compiled sprite resource does not match its manifest or exceeds the metadata budget.");
            }

            using MemoryStream input = new(bytes, writable: false);
            Guid id;
            Guid[] dependencies;
            if (entry.Kind == "model")
            {
                C3ModelData model = C3ModelCodec.Read(bytes);
                id = model.Definition.Id;
                models.Add(id, model);
                dependencies = [];
            }
            else if (entry.Kind == "atlas")
            {
                SpriteAtlas atlas = SpriteAssetCodec.ReadAtlas(input);
                atlases.Add(atlas);
                id = atlas.Id;
                dependencies = [atlas.TextureId];
            }
            else
            {
                SpriteAnimation animation = SpriteAssetCodec.ReadAnimation(input);
                animations.Add(animation);
                id = animation.Id;
                dependencies = [.. animation.Frames.Select(f => f.AtlasId).Distinct().Order()];
            }

            if (id != entry.Id || !entry.Dependencies.SequenceEqual(dependencies))
            {
                throw new InvalidDataException("Compiled resource identity or dependency coverage is inconsistent.");
            }
        }

        if (atlases.Sum(a => a.Sprites.Length) > 4096 || animations.Sum(a => a.Frames.Length) > 8192)
        {
            throw new InvalidDataException("Compiled sprite graph exceeds its project budget.");
        }

        SpriteLibrary library = new(textures, atlases, animations);

        foreach (string path in Directory.EnumerateFileSystemEntries(directory))
        {
            AssetFileIO.RequireSafePath(path);
            if (!expected.Remove(Path.GetFileName(path)) || Directory.Exists(path))
            {
                throw new InvalidDataException("The build contains files or directories outside its manifest.");
            }
        }

        if (expected.Count != 0)
        {
            throw new InvalidDataException("The build is missing a declared file.");
        }

        return new ContentPackage(manifest, library, models);
    }


    private static string FileName(Guid id) => id.ToString("N") + ".octex";

    private static string ResourceFileName(Guid id, string kind) => id.ToString("N") + (kind switch
    {
        "model" => ".ocmodel",
        "atlas" => ".ocatlas",
        "animation" => ".ocanim",
        _ => throw new InvalidDataException("Unsupported compiled resource kind."),
    });
}
