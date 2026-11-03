namespace OpenConquer.Content;

/// <summary>
/// Defines the structural contract for client content paths before filesystem or package lookup.
/// </summary>
internal static class ClientContentPath
{
    private static readonly char[] s_pathSeparators = ['/', '\\'];

    /// <summary>
    /// Splits a relative content path into segments. Empty, "." and ".." segments are rejected rather than normalized.
    /// </summary>
    public static string[] ParseSegments(string contentPath, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentPath, parameterName);

        if (IsRootedPath(contentPath))
        {
            throw new ArgumentException("Client content paths must be relative to the client content root.", parameterName);
        }

        string[] segments = contentPath.Split(s_pathSeparators, StringSplitOptions.None);

        foreach (string segment in segments)
        {
            if (segment.Length == 0 || segment is "." or ".." || segment.Contains(':', StringComparison.Ordinal) || segment.Contains('\0', StringComparison.Ordinal))
            {
                throw new ArgumentException($"Client content path '{contentPath}' is not a valid relative content path.", parameterName);
            }
        }

        return segments;
    }

    /// <summary>
    /// Produces the slash normalized virtual path used by package lookup after structural validation.
    /// </summary>
    public static string NormalizeVirtualPath(string contentPath, string parameterName, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentPath, parameterName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLength);

        if (contentPath.Length > maximumLength)
        {
            throw new ArgumentException($"Client content paths must not exceed {maximumLength} characters.", parameterName);
        }

        string[] segments = ParseSegments(contentPath, parameterName);

        return string.Join('/', segments);
    }

    private static bool IsRootedPath(string path)
    {
        if (path[0] is '/' or '\\')
        {
            return true;
        }

        return path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':';
    }
}
