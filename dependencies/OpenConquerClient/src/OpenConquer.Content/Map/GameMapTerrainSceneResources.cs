using System.Collections.ObjectModel;
using OpenConquer.Content.Ani;

namespace OpenConquer.Content.Map;

public sealed record GameMapTerrainObjectBinding(GameMapTerrainObject Terrain, AniIndexSection Animation);

public sealed record GameMapTerrainScenePlacement(
    GameMapSceneryRecord Scenery, IReadOnlyList<GameMapTerrainObjectBinding> Objects);

/// <summary>
/// Placements preserve map file order, not depth order. Shared scene files reuse the same bound object list.
/// </summary>
public sealed class GameMapTerrainSceneResources
{
    private const int MaximumScenePathLength = 260;

    private GameMapTerrainSceneResources(GameMapTerrainScenePlacement[] placements, int sceneFileCount)
    {
        Placements = Array.AsReadOnly(placements);
        SceneFileCount = sceneFileCount;
    }

    public IReadOnlyList<GameMapTerrainScenePlacement> Placements
    {
        get;
    }

    public int SceneFileCount
    {
        get;
    }

    public static GameMapTerrainSceneResources Load(IClientContentSource source, GameMapContent map,
        AniSectionResolver? animations = null)
    {
        animations ??= new AniSectionResolver(source, ContentLookupMode.LooseOnly);
        Dictionary<string, IReadOnlyList<GameMapTerrainObjectBinding>> sceneFiles = new(StringComparer.OrdinalIgnoreCase);
        List<GameMapTerrainScenePlacement> placements = [];

        foreach (GameMapSceneryRecord scenery in map.Scene.Scenery)
        {
            if (scenery.TypeCode != 1)
            {
                continue;
            }

            string scenePath = NormalizeScenePath(scenery.ResourcePath);
            if (!sceneFiles.TryGetValue(scenePath, out IReadOnlyList<GameMapTerrainObjectBinding>? objects))
            {
                objects = LoadSceneFile(source, animations, scenePath);
                sceneFiles.Add(scenePath, objects);
            }

            placements.Add(new GameMapTerrainScenePlacement(scenery, objects));
        }

        return new GameMapTerrainSceneResources([.. placements], sceneFiles.Count);
    }

    private static ReadOnlyCollection<GameMapTerrainObjectBinding> LoadSceneFile(IClientContentSource source,
        AniSectionResolver animations, string scenePath)
    {
        byte[] bytes = ContentReader.ReadRequiredBytes(source, scenePath, ContentLookupMode.LooseOnly,
            GameMapTerrainSceneFile.MaximumFileBytes);
        GameMapTerrainSceneFile file = GameMapTerrainSceneFile.Parse(bytes);

        GameMapTerrainObjectBinding[] bindings = new GameMapTerrainObjectBinding[file.Objects.Count];
        for (int index = 0; index < bindings.Length; index++)
        {
            GameMapTerrainObject terrainObject = file.Objects[index];
            bindings[index] = new GameMapTerrainObjectBinding(
                terrainObject, animations.Resolve(terrainObject.AnimationPath, terrainObject.SectionName));
        }

        return Array.AsReadOnly(bindings);
    }

    private static string NormalizeScenePath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new InvalidDataException("Type-1 map scenery has no scene file path.");
        }

        try
        {
            return ClientContentPath.NormalizeVirtualPath(path, nameof(path), MaximumScenePathLength);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException($"Type-1 map scenery scene path '{path}' is invalid.", exception);
        }
    }
}
