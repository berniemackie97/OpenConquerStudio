using OpenConquerStudio.Runtime;

namespace OpenConquerStudio.Authoring;

public static class StudioCompiler
{
    public const string ManifestName = "content.json";

    public static ContentBuild Compile(StudioProject project, string destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        project.ThrowIfDisposed();
        if (project.IsModified)
        {
            throw new InvalidOperationException("Save the project before compiling its content.");
        }

        destination = ProjectFiles.RequireSafePath(destination);
        string relative = Path.GetRelativePath(project.Root, destination).Replace('\\', '/');
        if (relative != ".." && !relative.StartsWith("../", StringComparison.Ordinal) &&
            !Path.IsPathRooted(relative) && !relative.StartsWith("Builds/", StringComparison.Ordinal))
        {
            throw new IOException("Builds inside a project must use the Builds directory.");
        }

        if (Directory.Exists(destination) || File.Exists(destination))
        {
            throw new IOException("The build destination already exists. Choose a new directory.");
        }

        string parent = Path.GetDirectoryName(destination) ?? throw new IOException("A build destination requires a parent directory.");
        ProjectFiles.RequireSafePath(parent);
        Directory.CreateDirectory(parent);

        string stage = Path.Combine(parent, $".studio-build-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stage);
        try
        {
            List<CompiledTexture> entries = [];
            foreach (TextureAsset asset in project.Document.Assets.OrderBy(a => a.Id))
            {
                cancellationToken.ThrowIfCancellationRequested();
                TextureData texture = project.Preview(asset.Id, cancellationToken);
                string path = Path.Combine(stage, FileName(asset.Id));
                using (FileStream output = new(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                    TextureCodec.Write(output, asset.Id, texture);
                    output.Flush(flushToDisk: true);
                }

                byte[] bytes = ProjectFiles.Read(path, TextureData.MaximumPixelBytes + 68);
                entries.Add(new CompiledTexture(asset.Id, asset.Name, texture.Width, texture.Height, bytes.Length, ProjectFiles.Hash(bytes)));
            }

            List<CompiledResource> resources = [];
            foreach (ResourceInfo info in project.Document.Resources.Where(a => a.Kind != "texture"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                using MemoryStream output = new();
                if (info.Kind == "model")
                {
                    output.Write(project.ModelBytes(info.Id).Span);
                }
                else if (info.Kind == "atlas")
                {
                    AtlasAsset asset = project.Document.Atlases.Single(a => a.Id == info.Id);
                    SpriteAssetCodec.WriteAtlas(output, new SpriteAtlas(asset.Id, asset.TextureId, asset.Sprites));
                }
                else
                {
                    AnimationAsset asset = project.Document.Animations.Single(a => a.Id == info.Id);
                    SpriteAssetCodec.WriteAnimation(output, new SpriteAnimation(asset.Id, asset.TicksPerSecond, asset.Repeat, asset.Frames));
                }

                byte[] bytes = output.ToArray();
                ProjectFiles.WriteAtomic(Path.Combine(stage, ResourceFileName(info.Id, info.Kind)), bytes, overwrite: false);
                resources.Add(new CompiledResource(info.Id, info.Name, info.Kind, bytes.Length, ProjectFiles.Hash(bytes), project.Document.Dependencies(info.Id)));
            }

            ContentBuild manifest = new(2, project.Document.Id, [.. entries], [.. resources]);
            ProjectFiles.WriteAtomic(Path.Combine(stage, ManifestName), ProjectFiles.Serialize(manifest), overwrite: false);
            Validate(stage, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ProjectFiles.RequireSafePath(destination);
            Directory.Move(stage, destination);
            return manifest;
        }
        finally
        {
            if (Directory.Exists(stage))
            {
                Directory.Delete(stage, recursive: true);
            }
        }
    }

    public static ContentBuild Validate(string directory, CancellationToken cancellationToken = default) =>
        ContentPackage.Load(directory, cancellationToken).Manifest;

    private static string FileName(Guid id) => id.ToString("N") + ".octex";

    private static string ResourceFileName(Guid id, string kind) => id.ToString("N") + (kind switch
    {
        "model" => ".ocmodel",
        "atlas" => ".ocatlas",
        "animation" => ".ocanim",
        _ => throw new InvalidDataException("Unsupported compiled resource kind."),
    });
}
