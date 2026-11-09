using System.Collections.Immutable;
using OpenConquer.Content.Map;
using OpenConquerStudio.Authoring;

namespace OpenConquerStudio.ConquerImport;

public static class ClientTerrainImporter
{
    public static TerrainImport Read(GameMapContent map, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(map);
        return Read(map.MapDataId, map.ArchivePath, map.CompanionValue, map.CellGrid, map.Scene, token);
    }

    public static TerrainImport Read(uint mapDataId, string mapPath, uint catalogValue,
        GameMapCellGrid grid, GameMapSceneSections scene, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(scene);
        long count = (long)grid.Width * grid.Height;
        if (grid.Width > 1024 || grid.Height > 1024 || count > ProjectDocument.MaximumTotalTerrainCells ||
            scene.ExitMarkers.Count > ProjectDocument.MaximumTerrainExitMarkers)
        {
            throw new InvalidDataException("The source terrain exceeds the authored grid or exit marker budget.");
        }

        ImmutableArray<TerrainCell>.Builder cells = ImmutableArray.CreateBuilder<TerrainCell>(checked((int)count));
        for (int y = 0; y < grid.Height; y++)
        {
            token.ThrowIfCancellationRequested();
            for (int x = 0; x < grid.Width; x++)
            {
                GameMapCell cell = grid.GetCell(x, y);
                cells.Add(new TerrainCell(cell.SurfaceId, cell.PassabilityFlag, cell.Elevation));
            }
        }

        ImmutableArray<TerrainExitMarker>.Builder exits = ImmutableArray.CreateBuilder<TerrainExitMarker>(scene.ExitMarkers.Count);
        foreach (GameMapExitMarker marker in scene.ExitMarkers)
        {
            token.ThrowIfCancellationRequested();
            exits.Add(new TerrainExitMarker(Guid.NewGuid(), marker.Field0, marker.Field1,
                unchecked((uint)marker.Field2)));
        }

        TerrainImport result = new(mapDataId, grid.Width, grid.Height, cells.MoveToImmutable(),
            exits.MoveToImmutable(), new TerrainSource(mapPath, grid.PuzzlePath,
                catalogValue, grid.HeaderField0, grid.HeaderField1, scene.Scenery.Count, scene.PositionedOverlays.Count));
        result.Validate();
        return result;
    }
}
