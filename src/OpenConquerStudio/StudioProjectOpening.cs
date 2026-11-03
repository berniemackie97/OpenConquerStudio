using System.Collections.Immutable;
using System.Text.Json;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.ConquerImport;

namespace OpenConquerStudio;

internal sealed record ProjectLocation(string Workspace, string? ClientRepository);
internal sealed record OpenedProject(StudioProject Project, ClientProjectConnection? Connection,
    ImmutableArray<ClientAssetEntry> Inventory, string? Warning);

/// <summary>Opens a candidate completely before the window releases the active project lease.</summary>
internal static class StudioProjectOpening
{
    internal static ProjectLocation Resolve(string path)
    {
        path = Path.GetFullPath(path);
        if (File.Exists(path))
        {
            if (!string.Equals(Path.GetFileName(path), "project.ocstudio", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Choose the project's project.ocstudio file.");
            }
            return new ProjectLocation(Path.GetDirectoryName(path)!, null);
        }
        if (File.Exists(Path.Combine(path, "project.ocstudio")))
        {
            return new ProjectLocation(path, null);
        }
        return ClientProjectConnection.IsClientRepository(path)
            ? new ProjectLocation(Path.Combine(path, ".studio", "authoring"), path)
            : new ProjectLocation(path, null);
    }

    internal static OpenedProject Open(ProjectLocation location, CancellationToken token)
    {
        StudioProject project = location.ClientRepository is { } client
            ? ClientProjectConnection.OpenWorkspace(client, token) : StudioProject.Open(location.Workspace, token);
        try
        {
            ClientProjectConnection? connection = ClientProjectConnection.OpenAttached(project.Root);
            if (connection is null && location.ClientRepository is { } repository)
            {
                connection = ClientProjectConnection.Attach(project, repository,
                    Path.Combine(repository, "content", "retail-5517", "payload"), token);
            }
            return new OpenedProject(project, connection, connection?.Inventory(token) ?? [], null);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            // Authored content remains editable when an optional local source installation is missing.
            return new OpenedProject(project, null, [], "Client sources unavailable: " + error.Message + " Reconnect with Client content folder. Your authored assets are available.");
        }
        catch { project.Dispose(); throw; }
    }
}
