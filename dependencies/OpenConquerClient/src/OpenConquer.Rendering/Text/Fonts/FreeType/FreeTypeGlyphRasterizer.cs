using System.Runtime.InteropServices;
using System.Text;
using OpenConquer.Rendering.Text.Glyphs;
using OpenConquer.Rendering.Text.Native;

namespace OpenConquer.Rendering.Text.Fonts.FreeType;

/// <summary>
/// Rasterizes glyphs from one configured FreeType face.
/// </summary>
internal sealed unsafe class FreeTypeGlyphRasterizer : IGlyphRasterizer
{
    private readonly FreeTypeFace _face;
    private readonly int _nominalPixelHeight;
    private readonly FreeTypeRenderMode _renderMode;
    private readonly int _loadFlags;
    private readonly int _baselinePixels;

    public FreeTypeGlyphRasterizer(FreeTypeLibrary library, ResolvedFont font, int nominalPixelHeight, bool antialiasEnabled,
        GlyphHinting hinting = GlyphHinting.Default)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(font);

        if (nominalPixelHeight == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nominalPixelHeight), nominalPixelHeight, "Nominal pixel height cannot be zero.");
        }

        FreeTypeFace face = new(library, font);

        try
        {
            ConfigureFace(face, font, nominalPixelHeight);
        }
        catch
        {
            face.Dispose();
            throw;
        }

        _face = face;
        _nominalPixelHeight = nominalPixelHeight;
        _renderMode = antialiasEnabled ? FreeTypeRenderMode.Normal : FreeTypeRenderMode.Mono;
        _loadFlags = hinting == GlyphHinting.Gdi ? FreeTypeConstants.LoadTargetMono : FreeTypeConstants.LoadDefault;
        _baselinePixels = hinting == GlyphHinting.Gdi ? GdiAscentPixels(face, nominalPixelHeight) : nominalPixelHeight;
    }

    public bool AntialiasEnabled => _renderMode == FreeTypeRenderMode.Normal;

    public bool TryRasterizeGlyph(Rune rune, out RasterizedGlyph? glyph)
    {
        glyph = _face.UseHandle(face => RasterizeGlyph((FreeTypeFaceRecord*)face, rune));
        return glyph is not null;
    }

    public void Dispose()
    {
        _face.Dispose();
    }

    private static void ConfigureFace(FreeTypeFace face, ResolvedFont font, int nominalPixelHeight)
    {
        int characterHeight26Dot6 = checked(nominalPixelHeight * 64);

        face.UseHandle(nativeFace =>
        {
            FreeTypeFaceRecord* faceRecord = (FreeTypeFaceRecord*)nativeFace;
            int error = FreeTypeNative.SetCharSize(faceRecord, new CLong(0), new CLong(characterHeight26Dot6), 0, 0);

            if (error != 0)
            {
                throw new FreeTypeFaceCreationException($"FreeType could not configure face {font.FaceIndex} from font '{font.FilePath}' at nominal height {nominalPixelHeight} (error {error}).");
            }

            return true;
        });
    }

    private RasterizedGlyph? RasterizeGlyph(FreeTypeFaceRecord* face, Rune character)
    {
        uint glyphIndex = FreeTypeNative.GetCharIndex(face, new CULong(checked((uint)character.Value)));

        if (glyphIndex == 0)
        {
            return null;
        }

        int error = FreeTypeNative.LoadGlyph(face, glyphIndex, _loadFlags);

        if (error != 0)
        {
            throw new InvalidOperationException($"FreeType could not load glyph U+{character.Value:X4} (error {error}).");
        }

        FreeTypeGlyphSlotRecord* glyphSlot = face->Glyph;

        if (glyphSlot is null)
        {
            throw new InvalidOperationException($"FreeType returned no glyph slot after loading U+{character.Value:X4}.");
        }

        error = FreeTypeNative.RenderGlyph(glyphSlot, _renderMode);

        if (error != 0)
        {
            throw new InvalidOperationException($"FreeType could not rasterize glyph U+{character.Value:X4} (error {error}).");
        }

        FreeTypeBitmap bitmap = glyphSlot->Bitmap;
        int widthPixels = checked((int)bitmap.Width);
        int heightPixels = checked((int)bitmap.Rows);
        byte[] coverage = FreeTypeBitmapNormalizer.CopyCoverage(bitmap);

        return new RasterizedGlyph(widthPixels, heightPixels, glyphSlot->BitmapLeft, checked(_baselinePixels - glyphSlot->BitmapTop), Convert26Dot6ToPixels(glyphSlot->Advance.X), coverage);
    }

    private static int GdiAscentPixels(FreeTypeFace face, int nominalPixelHeight) => face.UseHandle(nativeFace =>
    {
        FreeTypeFaceRecord* record = (FreeTypeFaceRecord*)nativeFace;
        return record->UnitsPerEm == 0 ? nominalPixelHeight
            : (int)Math.Ceiling(record->Ascender * (double)Math.Abs(nominalPixelHeight) / record->UnitsPerEm);
    });

    private static int Convert26Dot6ToPixels(CLong value)
    {
        return checked((int)(value.Value.ToInt64() / 64));
    }
}
