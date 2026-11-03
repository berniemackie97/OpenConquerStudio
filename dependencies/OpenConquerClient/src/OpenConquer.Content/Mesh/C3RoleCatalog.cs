namespace OpenConquer.Content.Mesh;

public readonly record struct C3ResolvedPart(
    string Name, string MeshPath, string TexturePath, string MotionDatabase,
    byte State, byte SourceBlend, byte DestinationBlend, byte Material,
    uint ObjectId = 0, uint PrimaryTextureId = 0, uint SecondaryTextureId = 0, string? SecondaryTexturePath = null);

public sealed class C3RoleCatalog
{
    private const int MaximumDatabaseBytes = 16 * 1024 * 1024;
    private readonly C3DatabaseArchive _archive;
    private readonly C3RolePartTables _configuration;
    private readonly Dictionary<uint, string> _meshPaths;
    private readonly Dictionary<uint, string> _texturePaths;
    private readonly Dictionary<string, Dictionary<uint, C3PartDescriptor[]>> _meshes =
        new(StringComparer.Ordinal);

    private C3RoleCatalog(C3DatabaseArchive archive, C3RolePartTables configuration,
        Dictionary<uint, string> meshPaths, Dictionary<uint, string> texturePaths)
    {
        _archive = archive;
        _configuration = configuration;
        _meshPaths = meshPaths;
        _texturePaths = texturePaths;
    }

    public static C3RoleCatalog Load(IClientContentSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        C3DatabaseArchive archive = C3DatabaseArchive.Load(source);
        C3RolePartTables configuration = C3RolePartTables.Read(
            archive.ReadRequired("ini/RolePart.dbc").Span);
        Dictionary<uint, string> meshes = C3ResourcePathTable.Read(
            ReadCoreDatabase(source, archive, "ini/3DObj.dbc"));
        Dictionary<uint, string> textures = C3ResourcePathTable.Read(
            ReadCoreDatabase(source, archive, "ini/3DTexture.dbc"));
        return new C3RoleCatalog(archive, configuration, meshes, textures);
    }

    public bool TryResolve(string partName, uint modelId, out IReadOnlyList<C3ResolvedPart> parts)
    {
        if (!_configuration.Sources.TryGetValue(partName, out C3RolePartSource source))
        {
            parts = [];
            return false;
        }

        if (!_meshes.TryGetValue(source.MeshDatabase, out Dictionary<uint, C3PartDescriptor[]>? models))
        {
            if (!_archive.TryRead(source.MeshDatabase, out ReadOnlyMemory<byte> payload))
            {
                parts = [];
                return false;
            }

            models = C3RolePartTables.ReadMeshes(payload.Span);
            _meshes.Add(source.MeshDatabase, models);
        }

        if (!models.TryGetValue(modelId, out C3PartDescriptor[]? entries))
        {
            parts = [];
            return false;
        }

        C3ResolvedPart[] result = new C3ResolvedPart[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            C3PartDescriptor entry = entries[i];
            if (!_meshPaths.TryGetValue(entry.ObjectId, out string? meshPath) ||
                !_texturePaths.TryGetValue(entry.PrimaryTextureId, out string? texturePath))
            {
                throw new InvalidDataException($"Role-part '{partName}' has an unresolved mesh or texture reference.");
            }

            result[i] = new C3ResolvedPart(partName, meshPath, texturePath, source.MotionDatabase,
                entry.State, entry.SourceBlend, entry.DestinationBlend, entry.Material,
                entry.ObjectId, entry.PrimaryTextureId, entry.SecondaryTextureId,
                _texturePaths.GetValueOrDefault(entry.SecondaryTextureId));
        }

        parts = result;
        return true;
    }

    public bool TryResolveMotionPath(string partName, ulong key, out string path)
    {
        if (_configuration.Sources.TryGetValue(partName, out C3RolePartSource source) &&
            _archive.TryRead(source.MotionDatabase, out ReadOnlyMemory<byte> payload))
        {
            return C3ResourcePathTable.TryFindDualKey(payload, key, out path);
        }

        path = string.Empty;
        return false;
    }

    public IReadOnlyList<string> VirtualMeshNames => [.. _configuration.VirtualMeshNames.Order(StringComparer.Ordinal)];

    public bool IsAttachmentMesh(string name) => _configuration.VirtualMeshNames.Contains(name);

    private static ReadOnlyMemory<byte> ReadCoreDatabase(IClientContentSource source,
        C3DatabaseArchive archive, string path)
    {
        if (source.TryOpenRead(path, ContentLookupMode.LooseOnly, out Stream? stream))
        {
            using (stream)
            {
                return ContentReader.ReadBytes(stream, path, MaximumDatabaseBytes);
            }
        }

        return archive.ReadRequired(path);
    }
}
