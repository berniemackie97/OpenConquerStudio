namespace OpenConquer.Content;

/// <summary>
/// Defines one runtime content dependency and the storage lookup policy required by its consumer.
/// </summary>
public sealed record ClientContentRequirement
{
    public ClientContentRequirement(string contentPath, ContentLookupMode lookupMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentPath);

        if (!Enum.IsDefined(lookupMode))
        {
            throw new ArgumentOutOfRangeException(nameof(lookupMode), lookupMode, "Unknown content lookup mode.");
        }

        _ = ClientContentPath.ParseSegments(contentPath, nameof(contentPath));

        ContentPath = contentPath;
        LookupMode = lookupMode;
    }

    public string ContentPath
    {
        get;
    }

    public ContentLookupMode LookupMode
    {
        get;
    }
}
