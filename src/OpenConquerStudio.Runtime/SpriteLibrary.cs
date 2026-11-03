using System.Collections.Immutable;

namespace OpenConquerStudio.Runtime;

/// <summary>A validated runtime sprite graph. Resources remain CPU data until a rendering host uploads them.</summary>
public sealed class SpriteLibrary
{
    public SpriteLibrary(IEnumerable<KeyValuePair<Guid, TextureData>> textures, IEnumerable<SpriteAtlas> atlases, IEnumerable<SpriteAnimation> animations)
    {
        ArgumentNullException.ThrowIfNull(textures);
        ArgumentNullException.ThrowIfNull(atlases);
        ArgumentNullException.ThrowIfNull(animations);
        Textures = textures.ToImmutableDictionary();
        Atlases = atlases.ToImmutableDictionary(a => a.Id);
        Animations = animations.ToImmutableDictionary(a => a.Id);
        if (Textures.Keys.Any(id => id == Guid.Empty) || Textures.Values.Any(t => t is null) ||
            Textures.Keys.Concat(Atlases.Keys).Concat(Animations.Keys).Distinct().Count() != Textures.Count + Atlases.Count + Animations.Count)
        {
            throw new InvalidDataException("Runtime resource identities must be nonempty and unique across all kinds.");
        }

        foreach (SpriteAtlas atlas in Atlases.Values)
        {
            if (!Textures.TryGetValue(atlas.TextureId, out TextureData? texture))
            {
                throw new InvalidDataException("A runtime atlas references a missing texture.");
            }

            atlas.ValidateTexture(texture.Width, texture.Height);
        }

        foreach (SpriteAnimation animation in Animations.Values)
        {
            foreach (AnimationFrame frame in animation.Frames)
            {
                if (!Atlases.TryGetValue(frame.AtlasId, out SpriteAtlas? atlas))
                {
                    throw new InvalidDataException("A runtime animation references a missing atlas.");
                }

                atlas.Find(frame.SpriteId);
            }
        }
    }

    public ImmutableDictionary<Guid, TextureData> Textures
    {
        get;
    }
    public ImmutableDictionary<Guid, SpriteAtlas> Atlases
    {
        get;
    }
    public ImmutableDictionary<Guid, SpriteAnimation> Animations
    {
        get;
    }

    public (TextureData Texture, SpriteRegion Sprite) Sample(Guid animationId, long elapsedTicks)
    {
        if (!Animations.TryGetValue(animationId, out SpriteAnimation? animation))
        {
            throw new InvalidDataException("Animation does not exist in this runtime library.");
        }

        AnimationFrame frame = animation.Frames[animation.SampleTicks(elapsedTicks)];
        SpriteAtlas atlas = Atlases[frame.AtlasId];
        return (Textures[atlas.TextureId], atlas.Find(frame.SpriteId));
    }
}
