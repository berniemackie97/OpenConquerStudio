using OpenConquer.Content.Ani;

namespace OpenConquer.Content.Map;

public sealed record GameMapSceneryAniBinding(GameMapSceneryRecord Scenery, AniIndexSection Animation);

/// <summary>Resolves authored type-4 scenery ANI sections, without assigning any rendering semantics.</summary>
public static class GameMapSceneryAniBindings
{
    public static IReadOnlyList<GameMapSceneryAniBinding> Load(IClientContentSource source, GameMapSceneSections scene,
        AniSectionResolver? animations = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(scene);

        animations ??= new AniSectionResolver(source, ContentLookupMode.LooseOnly);
        List<GameMapSceneryAniBinding> bindings = [];

        foreach (GameMapSceneryRecord record in scene.Scenery)
        {
            if (record.TypeCode != 4 || IsEmptyAnimationRecord(record))
            {
                continue;
            }

            if (record.ResourcePath is not { Length: > 0 } path ||
                record.PartName is not { Length: > 0 } sectionName)
            {
                throw new InvalidDataException("Type-4 map scenery has no ANI path or section name.");
            }

            bindings.Add(new GameMapSceneryAniBinding(record, animations.Resolve(path, sectionName)));
        }

        return Array.AsReadOnly(bindings.ToArray());
    }

    public static bool IsEmptyAnimationRecord(GameMapSceneryRecord record) =>
        record.ResourcePath == "AniTitle=" &&
        record.TileX == 0 &&
        record.TileY == 0 &&
        (record.EncodedPayload.Length is 0x1A0 or 0x1A4) &&
        record.EncodedPayload.Span[0x18C..].IndexOfAnyExcept((byte)0) < 0;
}
