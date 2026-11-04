using System.Text.Json;

namespace OpenConquerStudio.Authoring;

public static class WorldDefinitionSourceExporter
{
    public static void Export(StudioProject project, string destination, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        project.ThrowIfDisposed();
        if (project.IsModified)
        {
            throw new InvalidOperationException("Save the project before exporting world definitions.");
        }
        if (project.Document.Worlds.IsEmpty)
        {
            throw new InvalidDataException("Create at least one authored world before exporting definitions.");
        }

        destination = ProjectFiles.RequireSafePath(destination);
        if (File.Exists(destination) || Directory.Exists(destination))
        {
            throw new IOException("The world-definition export destination already exists.");
        }

        token.ThrowIfCancellationRequested();
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", 1);
            writer.WriteStartArray("maps");
            foreach (WorldAsset world in project.Document.Worlds.OrderBy(world => world.MapId))
            {
                token.ThrowIfCancellationRequested();
                TerrainAsset terrain = project.Document.Terrains.Single(a => a.Id == world.TerrainId);
                writer.WriteStartObject();
                writer.WriteNumber("mapId", world.MapId);
                writer.WriteNumber("mapDataId", terrain.MapDataId);
                writer.WriteNumber("flags", world.Flags);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        token.ThrowIfCancellationRequested();
        ProjectFiles.WriteAtomic(destination, buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)), overwrite: false);
    }
}
