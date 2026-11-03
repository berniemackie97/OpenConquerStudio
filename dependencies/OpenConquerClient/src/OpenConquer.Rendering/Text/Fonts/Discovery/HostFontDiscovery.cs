namespace OpenConquer.Rendering.Text.Fonts.Discovery;

/// <summary>
/// Describes the font resources registered with the host font system and, when available, the hosts preferred GUI font resource.
/// </summary>
internal sealed class HostFontDiscovery
{
    public HostFontDiscovery(IEnumerable<HostFontReference> fontReferences, HostFontReference? defaultGuiFont = null)
    {
        ArgumentNullException.ThrowIfNull(fontReferences);

        List<HostFontReference> references = [];

        foreach (HostFontReference? reference in fontReferences)
        {
            if (reference is null)
            {
                throw new ArgumentException("Host font discovery cannot contain a null font reference.", nameof(fontReferences));
            }

            references.Add(reference);
        }

        FontReferences = references.AsReadOnly();
        DefaultGuiFont = defaultGuiFont;
    }

    /// <summary>
    /// Gets the font resources registered with the host font system.
    /// </summary>
    public IReadOnlyList<HostFontReference> FontReferences
    {
        get;
    }

    /// <summary>
    /// Gets the host's preferred GUI font resource when one can be resolved.
    /// </summary>
    public HostFontReference? DefaultGuiFont
    {
        get;
    }
}
