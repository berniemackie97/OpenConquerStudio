namespace OpenConquerStudio.Authoring;

public sealed partial class StudioProject
{
    public Guid CreateTerrain(string name, uint mapDataId, int width, int height, TerrainCell fill)
    {
        ThrowIfDisposed();
        byte[] cells = TerrainGrid.Create(width, height, fill);
        TerrainAsset terrain = new(Guid.NewGuid(), name, mapDataId, width, height, ProjectFiles.Hash(cells));
        ProjectDocument updated = Document with
        {
            Terrains = [.. Document.Terrains, terrain]
        };
        updated.Validate();
        _terrainCells.TryAdd(terrain.CellDigest, cells);
        Apply(updated);
        return terrain.Id;
    }

    public void EditTerrainExits(Guid id, ImmutableArray<TerrainExitMarker> exits)
    {
        ThrowIfDisposed();
        TerrainAsset terrain = Document.Terrains.Single(a => a.Id == id);
        if (exits.IsDefault)
        {
            throw new InvalidDataException("Terrain exit markers must be initialized.");
        }
        if (terrain.Exits.SequenceEqual(exits))
        {
            return;
        }

        ProjectDocument edited = Document with
        {
            Terrains = [.. Document.Terrains.Select(a => a.Id == id ? terrain with { Exits = exits } : a)]
        };
        Apply(edited);
    }

    public TerrainCell ReadTerrainCell(Guid id, int x, int y)
    {
        ThrowIfDisposed();
        TerrainAsset terrain = Document.Terrains.Single(a => a.Id == id);
        return TerrainGrid.GetCell(_terrainCells[terrain.CellDigest], terrain.Width, terrain.Height, x, y);
    }

    internal ReadOnlySpan<byte> ReadTerrainPayload(Guid id)
    {
        ThrowIfDisposed();
        TerrainAsset terrain = Document.Terrains.Single(a => a.Id == id);
        return _terrainCells[terrain.CellDigest];
    }

    public void EditTerrainCell(Guid id, int x, int y, TerrainCell cell, string name)
    {
        ThrowIfDisposed();
        TerrainAsset terrain = Document.Terrains.Single(a => a.Id == id);
        byte[] previous = _terrainCells[terrain.CellDigest];
        byte[] changed = previous;
        if (TerrainGrid.GetCell(previous, terrain.Width, terrain.Height, x, y) != cell)
        {
            changed = (byte[])previous.Clone();
            TerrainGrid.SetCell(changed, terrain.Width, terrain.Height, x, y, cell);
        }

        TerrainAsset edited = terrain with
        {
            Name = name,
            CellDigest = ReferenceEquals(changed, previous) ? terrain.CellDigest : ProjectFiles.Hash(changed)
        };
        if (edited == terrain)
        {
            return;
        }

        ProjectDocument document = Document with
        {
            Terrains = [.. Document.Terrains.Select(a => a.Id == id ? edited : a)]
        };
        document.Validate();
        _terrainCells.TryAdd(edited.CellDigest, changed);
        Apply(document);
    }
}
