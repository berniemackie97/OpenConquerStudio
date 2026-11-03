using System.Diagnostics.CodeAnalysis;

namespace OpenConquer.Content;

/// <summary>
/// Opens client content by virtual path without exposing host filesystem paths to consumers.
/// </summary>
public interface IClientContentSource
{
    /// <summary>
    /// Opens <paramref name="contentPath"/> under the requested <paramref name="mode"/>.
    /// </summary>
    bool TryOpenRead(string contentPath, ContentLookupMode mode, [NotNullWhen(true)] out Stream? stream);

    /// <summary>
    /// Opens <paramref name="contentPath"/> under the requested <paramref name="mode"/> or throws.
    /// </summary>
    Stream OpenRequiredRead(string contentPath, ContentLookupMode mode);
}
