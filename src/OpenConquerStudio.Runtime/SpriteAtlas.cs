using System.Collections.Immutable;

namespace OpenConquerStudio.Runtime;

public sealed record SpriteRegion(Guid Id, string Name, int X, int Y, int Width, int Height, int PivotX, int PivotY);

public sealed class SpriteAtlas
{
    public const int MaximumSprites = 1024;

    public SpriteAtlas(Guid id, Guid textureId, ImmutableArray<SpriteRegion> sprites)
    {
        if (id == Guid.Empty || textureId == Guid.Empty || sprites.IsDefaultOrEmpty || sprites.Length > MaximumSprites)
        {
            throw new InvalidDataException("A sprite atlas requires valid identities and 1–1024 sprites.");
        }

        HashSet<Guid> ids = [];
        foreach (SpriteRegion region in sprites)
        {
            if (region is null || region.Id == Guid.Empty || !ids.Add(region.Id) || string.IsNullOrWhiteSpace(region.Name) ||
                region.Name.Length > 128 || region.Name != region.Name.Trim() || region.Name.Any(char.IsControl) ||
                region.X < 0 || region.Y < 0 || region.Width <= 0 || region.Height <= 0 ||
                (long)region.X + region.Width > TextureData.MaximumDimension || (long)region.Y + region.Height > TextureData.MaximumDimension ||
                region.PivotX is < -4096 or > 4096 || region.PivotY is < -4096 or > 4096)
            {
                throw new InvalidDataException("Sprite identities, names, rectangles, or pixel pivots are invalid.");
            }
        }

        Id = id;
        TextureId = textureId;
        Sprites = sprites;
    }

    public Guid Id
    {
        get;
    }
    public Guid TextureId
    {
        get;
    }
    public ImmutableArray<SpriteRegion> Sprites
    {
        get;
    }

    public SpriteRegion Find(Guid spriteId) => Sprites.FirstOrDefault(region => region.Id == spriteId)
        ?? throw new InvalidDataException($"Sprite '{spriteId}' is not defined by atlas '{Id}'.");

    public void ValidateTexture(int width, int height)
    {
        TextureData.GetByteLength(width, height);
        if (Sprites.Any(region => (long)region.X + region.Width > width || (long)region.Y + region.Height > height))
        {
            throw new InvalidDataException("A sprite rectangle extends outside its referenced texture.");
        }
    }
}
