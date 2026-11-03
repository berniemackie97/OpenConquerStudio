namespace OpenConquer.Content.Wdf;

/// <summary>
/// Derives the routing prefixes that bind declared packages to virtual paths.
/// </summary>
internal static class WdfPackagePrefix
{
    /// <summary>
    /// Derives the prefix a declared package registers under.
    /// </summary>
    public static string FromDeclaredPackageName(string declaredName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(declaredName);

        string normalizedName = Normalize(declaredName);
        int lastDotIndex = normalizedName.LastIndexOf('.');

        return lastDotIndex < 0 ? normalizedName : normalizedName[..lastDotIndex];
    }

    /// <summary>
    /// Derives the prefix a virtual path routes through.
    /// </summary>
    public static string FromVirtualPath(string virtualPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(virtualPath);

        string normalizedPath = Normalize(virtualPath);
        int separatorIndex = normalizedPath.IndexOf('/', StringComparison.Ordinal);

        return separatorIndex < 0 ? normalizedPath : normalizedPath[..separatorIndex];
    }

    /// <summary>
    /// Normalizes package paths by folding ASCII <c>A-Z</c> to lowercase and mapping <c>'\'</c> to <c>'/'</c>.
    /// </summary>
    private static string Normalize(string value)
    {
        return string.Create(value.Length, value, static (destination, source) =>
        {
            for (int index = 0; index < source.Length; index++)
            {
                char character = source[index];

                destination[index] = character switch
                {
                    >= 'A' and <= 'Z' => (char)(character + ('a' - 'A')),
                    '\\' => '/',
                    _ => character,
                };
            }
        });
    }
}
