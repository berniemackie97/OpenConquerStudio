using System.Collections.Immutable;

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

    public Guid ImportTerrain(TerrainImport source, string name)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(source);
        source.Validate();
        byte[] bytes = TerrainGrid.Encode(source.Cells.AsSpan(), source.Width, source.Height);
        TerrainAsset terrain = new(Guid.NewGuid(), name, source.MapDataId, source.Width,
            source.Height, ProjectFiles.Hash(bytes))
        {
            Exits = source.Exits,
            Source = source.Source
        };
        ProjectDocument updated = Document with
        {
            Terrains = [.. Document.Terrains, terrain]
        };
        updated.Validate();
        _terrainCells.TryAdd(terrain.CellDigest, bytes);
        Apply(updated);
        return terrain.Id;
    }

    public void RefreshTerrain(Guid id, TerrainImport source)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(source);
        source.Validate();
        TerrainAsset original = Document.Terrains.Single(a => a.Id == id);
        if (original.Source is null || original.MapDataId != source.MapDataId)
        {
            throw new InvalidDataException("Refresh requires terrain extracted from the same map data ID.");
        }

        byte[] bytes = TerrainGrid.Encode(source.Cells.AsSpan(), source.Width, source.Height);
        Dictionary<(int X, int Y, uint Passway), Queue<Guid>> existing = [];
        foreach (TerrainExitMarker marker in original.Exits)
        {
            var key = (marker.X, marker.Y, marker.PasswayIndex);
            if (!existing.TryGetValue(key, out Queue<Guid>? identities))
            {
                identities = new Queue<Guid>();
                existing.Add(key, identities);
            }
            identities.Enqueue(marker.Id);
        }

        ImmutableArray<TerrainExitMarker> markers = [.. source.Exits.Select(marker =>
        {
            var key = (marker.X, marker.Y, marker.PasswayIndex);
            return existing.TryGetValue(key, out Queue<Guid>? identities) && identities.TryDequeue(out Guid oldId)
                ? marker with { Id = oldId } : marker;
        })];
        if (original.Exits.SequenceEqual(markers))
        {
            markers = original.Exits;
        }

        TerrainAsset refreshed = original with
        {
            Width = source.Width,
            Height = source.Height,
            CellDigest = ProjectFiles.Hash(bytes),
            Exits = markers,
            Source = source.Source
        };
        if (refreshed == original)
        {
            return;
        }

        ProjectDocument updated = Document with
        {
            Terrains = [.. Document.Terrains.Select(a => a.Id == id ? refreshed : a)]
        };
        updated.Validate();
        _terrainCells.TryAdd(refreshed.CellDigest, bytes);
        Apply(updated);
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

    public void EditTerrainCell(Guid id, int x, int y, TerrainCell cell, string name,
        ImmutableArray<TerrainExitMarker> exits = default) =>
        PaintTerrainRegion(id, new TerrainRegion(x, y, x, y), cell, name, exits);

    public void PaintTerrainRegion(Guid id, TerrainRegion region, TerrainCell cell, string name,
        ImmutableArray<TerrainExitMarker> exits = default)
    {
        ThrowIfDisposed();
        TerrainAsset terrain = Document.Terrains.Single(a => a.Id == id);
        region.Validate(terrain.Width, terrain.Height);
        byte[] original = _terrainCells[terrain.CellDigest];
        byte[]? painted = null;
        for (int y = region.StartY; y <= region.EndY; y++)
        {
            for (int x = region.StartX; x <= region.EndX; x++)
            {
                if (TerrainGrid.GetCell(original, terrain.Width, terrain.Height, x, y) == cell)
                {
                    continue;
                }

                painted ??= (byte[])original.Clone();
                TerrainGrid.SetCell(painted, terrain.Width, terrain.Height, x, y, cell);
            }
        }

        TerrainAsset edited = terrain with
        {
            Name = name,
            CellDigest = painted is null ? terrain.CellDigest : ProjectFiles.Hash(painted),
            Exits = exits.IsDefault || terrain.Exits.SequenceEqual(exits) ? terrain.Exits : exits
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
        if (painted is not null)
        {
            _terrainCells.TryAdd(edited.CellDigest, painted);
        }
        Apply(document);
    }
}
