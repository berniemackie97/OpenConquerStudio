namespace OpenConquerStudio.Authoring;

public sealed partial class StudioProject
{
    public Guid CreateWorld(string name, uint mapId, Guid terrainId, ulong flags)
    {
        ThrowIfDisposed();
        WorldAsset world = new(Guid.NewGuid(), name, mapId, terrainId, flags);
        ProjectDocument updated = Document with
        {
            Worlds = [.. Document.Worlds, world]
        };
        Apply(updated);
        return world.Id;
    }

    public void EditWorld(WorldAsset world)
    {
        ThrowIfDisposed();
        WorldAsset original = Document.Worlds.Single(a => a.Id == world.Id);
        if (original == world)
        {
            return;
        }

        Apply(Document with
        {
            Worlds = [.. Document.Worlds.Select(a => a.Id == world.Id ? world : a)]
        });
    }
}
