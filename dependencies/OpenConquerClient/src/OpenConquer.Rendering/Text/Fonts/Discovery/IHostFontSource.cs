namespace OpenConquer.Rendering.Text.Fonts.Discovery;

/// <summary>
/// Discovers font resources registered with the current host font system.
/// </summary>
internal interface IHostFontSource
{
    /// <summary>
    /// Discovers the host's registered font resources and preferred GUI font resource.
    /// </summary>
    HostFontDiscovery Discover();
}
