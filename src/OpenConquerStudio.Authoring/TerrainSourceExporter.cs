using System.Text.Json;

namespace OpenConquerStudio.Authoring;

public static class TerrainSourceExporter
{
    public static void Export(StudioProject project, Guid id, string destination, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        project.ThrowIfDisposed();
        if (project.IsModified)
        {
            throw new InvalidOperationException("Save the project before exporting terrain.");
        }

        TerrainAsset terrain = project.Document.Terrains.SingleOrDefault(a => a.Id == id)
            ?? throw new InvalidDataException("Select an authored terrain to export.");
        destination = ProjectFiles.RequireSafePath(destination);
        if (File.Exists(destination) || Directory.Exists(destination))
        {
            throw new IOException("The terrain export destination already exists.");
        }

        token.ThrowIfCancellationRequested();
        ReadOnlySpan<byte> cells = project.ReadTerrainPayload(id);
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", 1);
            writer.WriteNumber("mapDataId", terrain.MapDataId);
            writer.WriteNumber("width", terrain.Width);
            writer.WriteNumber("height", terrain.Height);
            writer.WriteStartArray("cells");
            for (int y = 0; y < terrain.Height; y++)
            {
                token.ThrowIfCancellationRequested();
                for (int x = 0; x < terrain.Width; x++)
                {
                    TerrainCell cell = TerrainGrid.GetCell(cells, terrain.Width, terrain.Height, x, y);
                    writer.WriteStartObject();
                    writer.WriteNumber("surfaceId", cell.SurfaceId);
                    writer.WriteNumber("passabilityFlag", cell.PassabilityFlag);
                    writer.WriteNumber("elevation", cell.Elevation);
                    writer.WriteEndObject();
                }
            }

            writer.WriteEndArray();
            writer.WriteStartArray("exits");
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        token.ThrowIfCancellationRequested();
        ProjectFiles.WriteAtomic(destination, buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)), overwrite: false);
    }
}
