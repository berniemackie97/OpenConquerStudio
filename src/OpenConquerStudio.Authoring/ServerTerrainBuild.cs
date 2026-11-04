using System.Diagnostics;

namespace OpenConquerStudio.Authoring;

public static class ServerTerrainBuild
{
    private static readonly TimeSpan s_maximumToolDuration = TimeSpan.FromMinutes(2);

    public static async Task BuildAsync(StudioProject project, Guid terrainId, string serverTool, string destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        project.ThrowIfDisposed();
        if (project.IsModified)
        {
            throw new InvalidOperationException("Save the project before compiling terrain.");
        }
        if (!project.Document.Terrains.Any(terrain => terrain.Id == terrainId))
        {
            throw new InvalidDataException("The selected terrain does not exist.");
        }

        string toolPath = ProjectFiles.RequireSafePath(serverTool);
        if (Path.GetFileName(toolPath) != "OpenConquer.GameData.Tool.dll" || !File.Exists(toolPath))
        {
            throw new FileNotFoundException("Supply the built OpenConquer.GameData.Tool.dll explicitly.", toolPath);
        }

        destination = StudioCompiler.RequireBuildDestination(project, destination);
        string parent = Path.GetDirectoryName(destination)
            ?? throw new IOException("The terrain build destination requires a parent directory.");
        ProjectFiles.RequireSafePath(parent);
        Directory.CreateDirectory(parent);
        string stage = Path.Combine(parent, $".studio-terrain-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stage);
        try
        {
            string source = Path.Combine(stage, "source.json");
            TerrainSourceExporter.Export(project, terrainId, source, cancellationToken);
            string output = Path.Combine(stage, "compiled");
            await InvokeToolAsync(toolPath, source, output, cancellationToken).ConfigureAwait(false);
            uint mapDataId = project.Document.Terrains.Single(terrain => terrain.Id == terrainId).MapDataId;
            ValidateOutput(output, mapDataId);
            cancellationToken.ThrowIfCancellationRequested();
            StudioCompiler.RequireBuildDestination(project, destination);
            Directory.Move(output, destination);
        }
        finally
        {
            if (Directory.Exists(stage))
            {
                Directory.Delete(stage, recursive: true);
            }
        }
    }

    private static async Task InvokeToolAsync(string tool, string source, string destination, CancellationToken cancellationToken)
    {
        using Process process = new();
        process.StartInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        process.StartInfo.ArgumentList.Add(tool);
        process.StartInfo.ArgumentList.Add("build-authored-terrain");
        process.StartInfo.ArgumentList.Add(source);
        process.StartInfo.ArgumentList.Add(destination);
        if (!process.Start())
        {
            throw new IOException("The server terrain compiler could not start.");
        }

        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(s_maximumToolDuration);
        try
        {
            try
            {
                await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new IOException("The server terrain compiler exceeded its execution limit.");
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidDataException($"The server terrain compiler exited with code {process.ExitCode}.");
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) when (process.HasExited) { }
            }
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static void ValidateOutput(string output, uint mapDataId)
    {
        if (!HasEntries(output, "collisions", "terrains"))
        {
            throw new InvalidDataException("The server terrain build did not produce the required output directories.");
        }

        string name = mapDataId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        foreach ((string directory, string extension) in new[]
        {
            ("terrains", ".ocbt"),
            ("collisions", ".ocmc")
        })
        {
            string folder = Path.Combine(output, directory);
            string fileName = name + extension;
            if (!HasEntries(folder, fileName))
            {
                throw new InvalidDataException($"The server terrain build did not produce exactly one '{extension}' artifact.");
            }

            string file = ProjectFiles.RequireSafePath(Path.Combine(folder, fileName));
            if (new FileInfo(file).Length == 0)
            {
                throw new InvalidDataException("The server terrain build produced an empty artifact.");
            }
        }
    }

    private static bool HasEntries(string directory, params string[] entries) =>
        Directory.Exists(directory) &&
        Directory.EnumerateFileSystemEntries(ProjectFiles.RequireSafePath(directory))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .SequenceEqual(entries.Order(StringComparer.Ordinal), StringComparer.Ordinal);
}
