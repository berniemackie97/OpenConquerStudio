using System.Collections.Immutable;

namespace OpenConquerStudio.Authoring;

public sealed record TerrainSource(string MapPath, string PuzzlePath, uint CatalogValue,
    uint GridHeader0, uint GridHeader1, int SceneryCount, int OverlayGroupCount)
{
    public void Validate()
    {
        ValidatePath(MapPath);
        ValidatePath(PuzzlePath);
        if (SceneryCount < 0 || OverlayGroupCount < 0)
        {
            throw new InvalidDataException("Terrain source record counts must be nonnegative.");
        }
    }

    private static void ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 260 ||
            path.Any(char.IsControl) || path.Contains('\\') ||
            path.Split('/').Any(segment => segment.Length == 0 || segment is "." or ".." || segment.Contains(':')))
        {
            throw new InvalidDataException("Terrain source path must be a safe relative content path.");
        }
    }
}

public sealed record TerrainImport(uint MapDataId, int Width, int Height,
    ImmutableArray<TerrainCell> Cells, ImmutableArray<TerrainExitMarker> Exits, TerrainSource Source)
{
    public void Validate()
    {
        if (MapDataId == 0 || Cells.IsDefault || Cells.Length != TerrainGrid.CellCount(Width, Height) ||
            Exits.IsDefault || Exits.Length > ProjectDocument.MaximumTerrainExitMarkers || Source is null)
        {
            throw new InvalidDataException("Terrain extraction has invalid identity, dimensions, cells, or exits.");
        }

        Source.Validate();
        HashSet<Guid> ids = [];
        foreach (TerrainExitMarker exit in Exits)
        {
            if (exit.Id == Guid.Empty || !ids.Add(exit.Id))
            {
                throw new InvalidDataException("Terrain extraction exit identities must be unique and nonempty.");
            }
        }
    }
}
