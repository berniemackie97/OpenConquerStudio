namespace OpenConquer.Content.Ani;

/// <summary>
/// Reuses indexes by normalized content path within one resource-loading operation.
/// </summary>
public sealed class AniSectionResolver
{
    private const int MaximumIndexPathLength = 260;

    private readonly IClientContentSource _source;
    private readonly ContentLookupMode _lookupMode;
    private readonly Dictionary<string, AniIndexFile> _indexes = new(StringComparer.OrdinalIgnoreCase);

    public AniSectionResolver(IClientContentSource source, ContentLookupMode lookupMode)
    {
        _source = source;
        _lookupMode = lookupMode;
    }

    public AniIndexSection Resolve(string indexPath, string sectionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        string normalizedPath = ClientContentPath.NormalizeVirtualPath(indexPath, nameof(indexPath), MaximumIndexPathLength);
        if (!_indexes.TryGetValue(normalizedPath, out AniIndexFile? index))
        {
            index = AniIndexFile.Load(_source, normalizedPath, _lookupMode);
            _indexes.Add(normalizedPath, index);
        }

        if (!index.TryGetSection(sectionName, out AniIndexSection? section))
        {
            throw new InvalidDataException($"Animation index '{normalizedPath}' does not define section [{sectionName}].");
        }

        if (!string.Equals(section.Name, sectionName, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Animation section [{sectionName}] in '{normalizedPath}' resolves to the colliding section [{section.Name}].");
        }

        if (section.FrameCount == 0)
        {
            throw new InvalidDataException($"Animation section [{sectionName}] in '{normalizedPath}' has no frames.");
        }

        return section;
    }
}
