namespace OpenConquer.Content.Map;

/// <summary>A map selected by the server's map-data ID and loaded from its retail archive.</summary>
public sealed class GameMapContent
{
    private GameMapContent(GameMapCatalog.Terrain terrain, GameMapCellGrid grid, GameMapSceneSections scene)
    {
        MapDataId = terrain.MapDataId;
        ArchivePath = terrain.Path;
        CompanionValue = terrain.CompanionValue;
        CellGrid = grid;
        Scene = scene;
    }

    public uint MapDataId
    {
        get;
    }

    public string ArchivePath
    {
        get;
    }

    public uint CompanionValue
    {
        get;
    }

    public GameMapCellGrid CellGrid
    {
        get;
    }

    public GameMapSceneSections Scene
    {
        get;
    }

    public static GameMapContent Load(IClientContentSource contentSource, GameMapCatalog catalog, uint mapDataId)
    {
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentNullException.ThrowIfNull(catalog);

        if (!catalog.TryGetTerrain(mapDataId, out GameMapCatalog.Terrain? terrain))
        {
            throw new InvalidDataException($"Map data ID {mapDataId} is absent from GameMap.dat.");
        }

        byte[] archive = ContentReader.ReadRequiredBytes(contentSource, terrain.Path,
            ContentLookupMode.LooseOnly, GameMapArchive.MaximumArchiveBytes);
        byte[] decoded = GameMapArchive.Decode(archive, terrain.Path);
        GameMapCellGrid grid = GameMapCellGrid.Parse(decoded);
        GameMapSceneSections scene = GameMapSceneSections.Parse(decoded, grid.BytesConsumed, grid.HeaderField0);

        return new GameMapContent(terrain, grid, scene);
    }
}
