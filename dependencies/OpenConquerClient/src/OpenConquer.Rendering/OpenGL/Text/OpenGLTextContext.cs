using System.Runtime.ExceptionServices;
using OpenConquer.Rendering.Text.Fonts;
using OpenConquer.Rendering.Text.Fonts.Discovery;
using OpenConquer.Rendering.Text.Fonts.FreeType;
using OpenConquer.Rendering.Text.Glyphs;
using OpenConquer.Rendering.Text.Layout;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Rendering.OpenGL.Text;

/// <summary>
/// One font that matches the 5517 client's text, with its glyph cache, layout source and OpenGL atlas.
/// </summary>
public sealed class OpenGLTextContext : IDisposable
{
    private readonly FreeTypeLibrary? _freeTypeLibrary;
    private readonly IGlyphRasterizer _rasterizer;
    private readonly NativeTextLayoutEngine _layoutEngine;
    private readonly OpenGLTextResource _resource;
    private bool _disposed;

    public OpenGLTextContext(OpenGLGraphicsDevice graphicsDevice, string? fontToken, int nominalPixelHeight, int effectiveCodePage, bool antialiasEnabled)
        : this(graphicsDevice, fontToken, nominalPixelHeight, effectiveCodePage, antialiasEnabled, bold: false)
    {
    }

    public OpenGLTextContext(OpenGLGraphicsDevice graphicsDevice, string? fontToken, int nominalPixelHeight, int effectiveCodePage, bool antialiasEnabled, bool bold)
        : this(graphicsDevice, fontToken, nominalPixelHeight, effectiveCodePage, antialiasEnabled, bold, GlyphHinting.Default)
    {
    }

    public OpenGLTextContext(OpenGLGraphicsDevice graphicsDevice, string? fontToken, int nominalPixelHeight, int effectiveCodePage, bool antialiasEnabled, bool bold,
        GlyphHinting hinting)
        : this(ValidateGraphicsDevice(graphicsDevice), CreateOwnedFont(fontToken, nominalPixelHeight, antialiasEnabled, bold, hinting), effectiveCodePage)
    {
    }

    internal OpenGLTextContext(OpenGLGraphicsDevice graphicsDevice, IGlyphRasterizer rasterizer, int nominalPixelHeight, int effectiveCodePage)
        : this(ValidateGraphicsDevice(graphicsDevice), CreateInjectedFont(rasterizer, nominalPixelHeight), effectiveCodePage)
    {
    }

    private OpenGLTextContext(OpenGLGraphicsDevice graphicsDevice, OwnedFont ownedFont, int effectiveCodePage)
    {
        OpenGLTextResource? resource = null;

        try
        {
            NativeTextFontRecord font = new(0, ownedFont.NominalPixelHeight, ownedFont.NominalPixelHeight, ownedFont.Rasterizer);
            NativeTextLayoutEngine layoutEngine = new(font, font, effectiveCodePage);
            resource = graphicsDevice.CreateTextResource(layoutEngine.Source);

            _freeTypeLibrary = ownedFont.Library;
            _rasterizer = ownedFont.Rasterizer;
            _layoutEngine = layoutEngine;
            _resource = resource;
        }
        catch
        {
            try
            {
                resource?.Dispose();
            }
            catch
            {
                // ignored
            }

            try
            {
                ownedFont.Dispose();
            }
            catch
            {
                // ignored
            }

            throw;
        }
    }

    public OpenGLTextLayout Layout(ReadOnlySpan<byte> encodedText, bool recognizeDataIcons = false, int dataIconWidthPixels = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new OpenGLTextLayout(this, _layoutEngine.Layout(encodedText, recognizeDataIcons, dataIconWidthPixels));
    }

    public OpenGLTextLayout LayoutUnicode(ReadOnlySpan<char> text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new OpenGLTextLayout(this, _layoutEngine.LayoutUnicode(text));
    }

    public void Draw(OpenGLRenderer renderer, OpenGLTextLayout layout, NativeTextRenderOptions options, int x, int y)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(layout);

        layout.ValidateOwner(this, nameof(layout));
        renderer.DrawText(_resource, layout.NativeLayout, options, x, y);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _resource.Dispose();
        }
        catch (Exception exception) { firstFailure = ExceptionDispatchInfo.Capture(exception); }

        try
        {
            _rasterizer.Dispose();
        }
        catch (Exception exception) { firstFailure ??= ExceptionDispatchInfo.Capture(exception); }

        try
        {
            _freeTypeLibrary?.Dispose();
        }
        catch (Exception exception) { firstFailure ??= ExceptionDispatchInfo.Capture(exception); }
        finally { _disposed = true; }

        firstFailure?.Throw();
    }

    private static OpenGLGraphicsDevice ValidateGraphicsDevice(OpenGLGraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        return graphicsDevice;
    }

    private static OwnedFont CreateOwnedFont(string? fontToken, int nominalPixelHeight, bool antialiasEnabled, bool bold, GlyphHinting hinting)
    {
        if (nominalPixelHeight == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nominalPixelHeight), nominalPixelHeight, "Nominal pixel height cannot be zero.");
        }

        FreeTypeLibrary library = new();

        try
        {
            HostFontDiscovery discovery = new SystemHostFontSource().Discover();
            HostFontCatalog catalog = new(new FreeTypeFontInspector(library), discovery);
            SystemFontResolver resolver = new(catalog);
            FreeTypeGlyphRasterizerFactory factory = new(library, resolver);
            IGlyphRasterizer rasterizer = factory.Create(fontToken, nominalPixelHeight, antialiasEnabled, bold, hinting);

            return new OwnedFont(library, rasterizer, nominalPixelHeight);
        }
        catch
        {
            try
            {
                library.Dispose();
            }
            catch
            {
                // ignored
            }

            throw;
        }
    }

    private static OwnedFont CreateInjectedFont(IGlyphRasterizer rasterizer, int nominalPixelHeight)
    {
        ArgumentNullException.ThrowIfNull(rasterizer);

        if (nominalPixelHeight == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nominalPixelHeight), nominalPixelHeight, "Nominal pixel height cannot be zero.");
        }

        return new OwnedFont(library: null, rasterizer, nominalPixelHeight);
    }

    private sealed class OwnedFont : IDisposable
    {
        public OwnedFont(FreeTypeLibrary? library, IGlyphRasterizer rasterizer, int nominalPixelHeight)
        {
            ArgumentNullException.ThrowIfNull(rasterizer);

            Library = library;
            Rasterizer = rasterizer;
            NominalPixelHeight = nominalPixelHeight;
        }

        public FreeTypeLibrary? Library
        {
            get;
        }

        public IGlyphRasterizer Rasterizer
        {
            get;
        }

        public int NominalPixelHeight
        {
            get;
        }

        public void Dispose()
        {
            ExceptionDispatchInfo? firstFailure = null;

            try
            {
                Rasterizer.Dispose();
            }
            catch (Exception exception) { firstFailure = ExceptionDispatchInfo.Capture(exception); }

            try
            {
                Library?.Dispose();
            }
            catch (Exception exception) { firstFailure ??= ExceptionDispatchInfo.Capture(exception); }

            firstFailure?.Throw();
        }
    }
}
