using OpenConquerStudio.ConquerImport;

namespace OpenConquerStudio;

internal static class StudioStartupProject
{
    internal static string? Resolve(string? explicitProject, string workingDirectory, string applicationDirectory)
    {
        if (explicitProject is not null)
        {
            return Path.GetFullPath(explicitProject, workingDirectory);
        }

        return FindParentProject(workingDirectory) ?? FindParentProject(applicationDirectory);
    }

    private static string? FindParentProject(string directory)
    {
        string? current = Path.GetFullPath(directory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "project.ocstudio")) || ClientProjectConnection.IsClientRepository(current))
            {
                return current;
            }

            current = Path.GetDirectoryName(current);
        }

        return null;
    }
}
