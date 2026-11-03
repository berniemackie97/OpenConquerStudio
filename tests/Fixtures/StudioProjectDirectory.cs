namespace OpenConquerStudio.Tests;

using OpenConquerStudio.Authoring;

internal sealed class ProjectDirectory : IDisposable
{
    public static string RepositoryRoot
    {
        get
        {
            string? directory = AppContext.BaseDirectory;
            while (directory is not null && !File.Exists(Path.Combine(directory, "OpenConquerStudio.slnx")))
            {
                directory = Path.GetDirectoryName(directory);
            }
            string client = Environment.GetEnvironmentVariable("OPENCONQUER_CLIENT_ROOT")
                ?? Path.Combine(directory ?? throw new DirectoryNotFoundException("The Studio source checkout is required for integration tests."),
                    ".fixtures", "client");
            client = Path.GetFullPath(client);
            Xunit.Assert.SkipUnless(File.Exists(Path.Combine(client, "src", "OpenConquer.Client", "OpenConquer.Client.csproj")),
                "Local client content is unavailable. Set OPENCONQUER_CLIENT_ROOT to run this integration test.");
            return client;
        }
    }

    public ProjectDirectory()
    {
        string temporary = OperatingSystem.IsMacOS() ? "/private/tmp" : Path.GetTempPath();
        Root = Path.Combine(temporary, "openconquerstudio-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Project);
    }

    public string Root
    {
        get;
    }
    public string Project => Path.Combine(Root, "project");
    public string Manifest => Path.Combine(Project, "project.ocstudio");
    public string PathFor(params string[] parts) => Path.Combine([Root, .. parts]);

    public string WriteTga()
    {
        byte[] tga = new byte[27];
        tga[2] = 10;
        tga[12] = 2;
        tga[14] = 1;
        tga[16] = 32;
        tga[17] = 8;
        tga[18] = 1;
        new byte[] { 0, 0, 255, 255, 0, 255, 0, 255 }.CopyTo(tga, 19);
        string path = PathFor("two-colors.tga");
        File.WriteAllBytes(path, tga);
        return path;
    }

    public void CreateSavedTexture()
    {
        using StudioProject project = StudioProject.Create(Project, "Game");
        project.CreateTexture("Color", 1, 1, 1, 2, 3);
        project.Save();
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
