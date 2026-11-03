using OpenConquer.Rendering.Text.Glyphs;

namespace OpenConquer.Rendering.Text.Fonts.FreeType;

/// <summary>
/// Creates glyph rasterizers using the configured font fallback chain.
/// </summary>
internal sealed class FreeTypeGlyphRasterizerFactory
{
    private const string SimSunCollectionToken = "$simsun.ttc";
    private const string SimSunFontToken = "$simsun.ttf";
    private const string CourierNewFamilyName = "Courier New";

    private readonly RasterizerCreator _rasterizerCreator;
    private readonly IFontResolver _fontResolver;

    public FreeTypeGlyphRasterizerFactory(FreeTypeLibrary library, IFontResolver fontResolver) : this(fontResolver, CreateRasterizerCreator(library))
    {
    }

    internal FreeTypeGlyphRasterizerFactory(IFontResolver fontResolver, RasterizerCreator rasterizerCreator)
    {
        ArgumentNullException.ThrowIfNull(fontResolver);
        ArgumentNullException.ThrowIfNull(rasterizerCreator);

        _fontResolver = fontResolver;
        _rasterizerCreator = rasterizerCreator;
    }

    public IGlyphRasterizer Create(string? fontToken, int nominalPixelHeight, bool antialiasEnabled, bool bold = false,
        GlyphHinting hinting = GlyphHinting.Default)
    {
        if (nominalPixelHeight == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nominalPixelHeight), nominalPixelHeight, "Nominal pixel height cannot be zero.");
        }

        Exception? lastFailure = null;

        // A bold family request selects that family's bold face; file tokens and families without one take the regular chain.
        if (bold && !string.IsNullOrEmpty(fontToken) && fontToken[0] != '$' &&
            _fontResolver.TryResolveBold(fontToken, out ResolvedFont? boldFont) && boldFont is not null &&
            TryCreate(boldFont, nominalPixelHeight, antialiasEnabled, hinting, ref lastFailure) is { } boldRasterizer)
        {
            return boldRasterizer;
        }

        if (!string.IsNullOrEmpty(fontToken) && TryCreateFromToken(fontToken, nominalPixelHeight, antialiasEnabled, hinting, ref lastFailure) is { } requestedRasterizer)
        {
            return requestedRasterizer;
        }

        if (_fontResolver.TryResolveDefaultGuiFont(out ResolvedFont? defaultGuiFont) && defaultGuiFont is not null && TryCreate(defaultGuiFont, nominalPixelHeight, antialiasEnabled, hinting, ref lastFailure) is { } defaultGuiRasterizer)
        {
            return defaultGuiRasterizer;
        }

        if (TryCreateFromToken(SimSunCollectionToken, nominalPixelHeight, antialiasEnabled, hinting, ref lastFailure) is { } simSunCollectionRasterizer)
        {
            return simSunCollectionRasterizer;
        }

        if (TryCreateFromToken(SimSunFontToken, nominalPixelHeight, antialiasEnabled, hinting, ref lastFailure) is { } simSunRasterizer)
        {
            return simSunRasterizer;
        }

        if (TryCreateFromToken(CourierNewFamilyName, nominalPixelHeight, antialiasEnabled, hinting, ref lastFailure) is { } courierNewRasterizer)
        {
            return courierNewRasterizer;
        }

        throw new InvalidOperationException("No usable font could be created from the requested font or fallback chain.", lastFailure);
    }

    internal delegate IGlyphRasterizer RasterizerCreator(ResolvedFont font, int nominalPixelHeight, bool antialiasEnabled, GlyphHinting hinting);

    private IGlyphRasterizer? TryCreateFromToken(string fontToken, int nominalPixelHeight, bool antialiasEnabled, GlyphHinting hinting, ref Exception? lastFailure)
    {
        if (!_fontResolver.TryResolve(fontToken, out ResolvedFont? font) || font is null)
        {
            return null;
        }

        return TryCreate(font, nominalPixelHeight, antialiasEnabled, hinting, ref lastFailure);
    }

    private IGlyphRasterizer? TryCreate(ResolvedFont font, int nominalPixelHeight, bool antialiasEnabled, GlyphHinting hinting, ref Exception? lastFailure)
    {
        try
        {
            return _rasterizerCreator(font, nominalPixelHeight, antialiasEnabled, hinting) ?? throw new InvalidOperationException("Glyph rasterizer creator returned null.");
        }
        catch (FreeTypeFaceCreationException exception)
        {
            lastFailure = exception;
        }
        catch (InvalidDataException exception)
        {
            lastFailure = exception;
        }
        catch (IOException exception)
        {
            lastFailure = exception;
        }
        catch (UnauthorizedAccessException exception)
        {
            lastFailure = exception;
        }

        return null;
    }

    private static RasterizerCreator CreateRasterizerCreator(FreeTypeLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);

        return (font, nominalPixelHeight, antialiasEnabled, hinting) => new FreeTypeGlyphRasterizer(library, font, nominalPixelHeight, antialiasEnabled, hinting);
    }
}
