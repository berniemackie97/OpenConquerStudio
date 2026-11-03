using System.Collections.Immutable;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio.Authoring;

public sealed record ImportedImageFrame(string Name, TextureData Texture, string Format, string SourceDigest, string SourceKey);

public sealed partial class StudioProject
{
    public Guid ImportSourceImage(ImportedImageFrame image)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(image);
        return AddTexture(image.Name, image.Texture, image.Format, image.SourceDigest, image.SourceKey);
    }

    public void ReplaceSourceImage(Guid id, ImportedImageFrame image)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(image);
        ReplaceTexture(FindAsset(id), image.Texture, image.Format, image.SourceDigest, image.SourceKey);
    }

    public Guid ImportAnimation(string name, ImmutableArray<ImportedImageFrame> images, int frameMilliseconds)
    {
        ThrowIfDisposed();
        if (images.IsDefaultOrEmpty || images.Length > MaximumImportFrames || frameMilliseconds < 1)
        {
            throw new InvalidDataException("Import 1–64 frames and choose a positive frame duration in milliseconds.");
        }

        List<TextureAsset> textures = [];
        List<AtlasAsset> atlases = [];
        List<AnimationFrame> frames = [];
        Dictionary<string, AtlasAsset> imported = new(StringComparer.Ordinal);
        Dictionary<string, TextureData> pixels = new(StringComparer.Ordinal);
        foreach (ImportedImageFrame image in images)
        {
            ArgumentNullException.ThrowIfNull(image);
            if (!imported.TryGetValue(image.SourceKey, out AtlasAsset? atlas))
            {
                TextureData texture = image.Texture;
                string digest = ProjectFiles.Hash(texture.Pixels.Span);
                TextureAsset asset = new(Guid.NewGuid(), image.Name, texture.Width, texture.Height, digest, image.Format, image.SourceDigest, false, 0, image.SourceKey);
                atlas = new AtlasAsset(Guid.NewGuid(), image.Name + " sprite", asset.Id,
                    [new SpriteRegion(Guid.NewGuid(), "Frame", 0, 0, texture.Width, texture.Height, texture.Width / 2, texture.Height / 2)]);
                textures.Add(asset);
                atlases.Add(atlas);
                imported.Add(image.SourceKey, atlas);
                pixels.TryAdd(digest, texture);
            }

            frames.Add(new AnimationFrame(Guid.NewGuid(), atlas.Id, atlas.Sprites[0].Id, frameMilliseconds));
        }

        AnimationAsset animation = new(Guid.NewGuid(), name, 1000, true, [.. frames]);
        ProjectDocument document = Document with
        {
            Assets = [.. Document.Assets, .. textures],
            Atlases = [.. Document.Atlases, .. atlases],
            Animations = [.. Document.Animations, animation],
        };
        document.Validate();
        foreach ((string digest, TextureData texture) in pixels)
        {
            _pixels.TryAdd(digest, texture);
        }

        Apply(document);
        return animation.Id;
    }

    public void ReimportAnimation(Guid id, ImmutableArray<ImportedImageFrame> images)
    {
        ThrowIfDisposed();
        AnimationAsset animation = Document.Animations.SingleOrDefault(a => a.Id == id)
            ?? throw new InvalidDataException("Select an existing animation to refresh.");
        if (images.IsDefaultOrEmpty || images.Length > MaximumImportFrames || images.Length != animation.Frames.Length)
        {
            throw new InvalidDataException("The source frame count changed or exceeds the import limit. Import a new clip.");
        }

        Dictionary<Guid, AtlasAsset> atlases = Document.Atlases.ToDictionary(a => a.Id);
        Dictionary<Guid, TextureAsset> textures = Document.Assets.ToDictionary(a => a.Id);
        Dictionary<Guid, TextureAsset> updated = [];
        Dictionary<string, TextureData> pixels = new(StringComparer.Ordinal);
        for (int index = 0; index < images.Length; index++)
        {
            ImportedImageFrame image = images[index] ?? throw new InvalidDataException("Source animation contains an empty frame.");
            AnimationFrame frame = animation.Frames[index];
            AtlasAsset atlas = atlases[frame.AtlasId];
            TextureAsset texture = textures[atlas.TextureId];
            if (atlas.Sprites.Length != 1 || atlas.Sprites[0].Id != frame.SpriteId ||
                texture.SourceKey is null || texture.SourceKey != image.SourceKey)
            {
                throw new InvalidDataException($"Frame {index} no longer matches its source image. Import a new clip.");
            }

            TextureAsset replacement = ReplacedTexture(texture, image.Texture, image.Format, image.SourceDigest, image.SourceKey);
            if (updated.TryGetValue(texture.Id, out TextureAsset? previous) && previous != replacement)
            {
                throw new InvalidDataException("Repeated source frames contain conflicting image data.");
            }

            updated[texture.Id] = replacement;
            pixels.TryAdd(replacement.PixelDigest, image.Texture);
        }

        if (updated.Values.All(asset => asset == textures[asset.Id]))
        {
            return;
        }

        ProjectDocument document = Document with
        {
            Assets = [.. Document.Assets.Select(asset => updated.GetValueOrDefault(asset.Id) ?? asset)]
        };
        document.Validate();
        foreach ((string digest, TextureData texture) in pixels)
        {
            _pixels.TryAdd(digest, texture);
        }
        Apply(document);
    }

    private const int MaximumImportFrames = 64;

    public Guid CreateAtlas(string name, Guid textureId)
    {
        ThrowIfDisposed();
        TextureAsset texture = FindAsset(textureId);
        bool rotated = texture.ClockwiseQuarterTurns % 2 != 0;
        int width = rotated ? texture.Height : texture.Width;
        int height = rotated ? texture.Width : texture.Height;
        AtlasAsset atlas = new(Guid.NewGuid(), name, textureId,
            [new SpriteRegion(Guid.NewGuid(), "Sprite", 0, 0, width, height, width / 2, height / 2)]);
        Apply(Document with
        {
            Atlases = [.. Document.Atlases, atlas]
        });
        return atlas.Id;
    }

    public void EditAtlas(AtlasAsset atlas)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(atlas);
        if (!Document.Atlases.Any(a => a.Id == atlas.Id))
        {
            throw new InvalidDataException("Select an existing atlas to edit.");
        }

        AtlasAsset original = Document.Atlases.Single(a => a.Id == atlas.Id);
        if (original.Name == atlas.Name && original.TextureId == atlas.TextureId && original.Sprites.SequenceEqual(atlas.Sprites))
        {
            return;
        }
        Apply(Document with
        {
            Atlases = Document.Atlases.Select(a => a.Id == atlas.Id ? atlas : a).ToImmutableArray()
        });
    }

    public Guid CreateAnimation(string name, Guid atlasId, int ticksPerSecond = 1000, int frameDuration = 100)
    {
        ThrowIfDisposed();
        AtlasAsset atlas = Document.Atlases.SingleOrDefault(a => a.Id == atlasId) ?? throw new InvalidDataException("Select an atlas first.");
        AnimationAsset animation = new(Guid.NewGuid(), name, ticksPerSecond, true,
            [.. atlas.Sprites.Select(s => new AnimationFrame(Guid.NewGuid(), atlas.Id, s.Id, frameDuration))]);
        Apply(Document with
        {
            Animations = [.. Document.Animations, animation]
        });
        return animation.Id;
    }

    public void EditAnimation(AnimationAsset animation)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(animation);
        if (!Document.Animations.Any(a => a.Id == animation.Id))
        {
            throw new InvalidDataException("Select an existing animation to edit.");
        }

        AnimationAsset original = Document.Animations.Single(a => a.Id == animation.Id);
        if (original.Name == animation.Name && original.TicksPerSecond == animation.TicksPerSecond && original.Repeat == animation.Repeat && original.Frames.SequenceEqual(animation.Frames))
        {
            return;
        }
        Apply(Document with
        {
            Animations = Document.Animations.Select(a => a.Id == animation.Id ? animation : a).ToImmutableArray()
        });
    }

    public void Delete(Guid id)
    {
        ThrowIfDisposed();
        ImmutableArray<ResourceInfo> dependents = Document.Dependents(id);
        if (!dependents.IsEmpty)
        {
            throw new InvalidDataException("Delete blocked by dependent resources: " + string.Join(", ", dependents.Select(a => a.Name)));
        }

        Apply(Document with
        {
            Assets = [.. Document.Assets.Where(a => a.Id != id)],
            Models = [.. Document.Models.Where(a => a.Id != id)],
            Atlases = [.. Document.Atlases.Where(a => a.Id != id)],
            Animations = [.. Document.Animations.Where(a => a.Id != id)],
            Terrains = [.. Document.Terrains.Where(a => a.Id != id)],
        });
    }

    public (TextureData Texture, SpriteRegion Region) PreviewSprite(Guid atlasId, Guid spriteId, CancellationToken token = default)
    {
        ThrowIfDisposed();
        AtlasAsset atlas = Document.Atlases.FirstOrDefault(a => a.Id == atlasId) ?? throw new InvalidDataException("Atlas does not exist.");
        SpriteRegion sprite = atlas.Sprites.FirstOrDefault(s => s.Id == spriteId) ?? throw new InvalidDataException("Sprite does not exist.");
        return (Preview(atlas.TextureId, token), sprite);
    }
}
