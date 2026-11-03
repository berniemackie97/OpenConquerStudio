namespace OpenConquer.Content;

/// <summary>
/// Selects which storage a content lookup is allowed to satisfy.
/// </summary>
public enum ContentLookupMode
{
    /// <summary>
    /// Resolve a loose file first and fall back to a registered package.
    /// </summary>
    LooseThenPackage = 0,

    /// <summary>
    /// Resolve only a loose file on the host filesystem.
    /// </summary>
    LooseOnly = 1,

    /// <summary>
    /// Resolve only an entry inside a registered package.
    /// </summary>
    PackageOnly = 2,
}
