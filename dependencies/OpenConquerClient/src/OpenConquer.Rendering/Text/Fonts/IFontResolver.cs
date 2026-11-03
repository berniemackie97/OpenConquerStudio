namespace OpenConquer.Rendering.Text.Fonts;

/// <summary>
/// Resolves font tokens and the host GUI fallback font.
/// </summary>
internal interface IFontResolver
{
    bool TryResolve(string fontToken, out ResolvedFont? font);

    /// <summary>Resolves the upright bold face of a family, as a weight-700 GDI request selects it.</summary>
    bool TryResolveBold(string familyName, out ResolvedFont? font);

    bool TryResolveDefaultGuiFont(out ResolvedFont? font);
}
