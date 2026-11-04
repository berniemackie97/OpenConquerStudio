namespace OpenConquerStudio.Authoring;

public sealed partial record ProjectDocument
{
    private void ValidateWorlds(HashSet<Guid> ids)
    {
        HashSet<uint> mapIds = [];
        HashSet<Guid> terrainIds = Terrains.Select(terrain => terrain.Id).ToHashSet();
        foreach (WorldAsset world in Worlds)
        {
            if (world is null || world.Id == Guid.Empty || !ids.Add(world.Id) || world.MapId == 0 ||
                !mapIds.Add(world.MapId))
            {
                throw new InvalidDataException("World identities and map IDs must be nonempty and unique.");
            }

            ValidateName(world.Name);
            if (!terrainIds.Contains(world.TerrainId))
            {
                throw new InvalidDataException($"World '{world.Name}' references an unavailable terrain.");
            }
        }
    }
}
