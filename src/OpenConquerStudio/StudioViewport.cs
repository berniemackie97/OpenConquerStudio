using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using OpenConquer.Content;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;
using OpenConquerStudio.ConquerRendering;
using OpenConquerStudio.Runtime;
using Silk.NET.OpenGL;

namespace OpenConquerStudio;

internal sealed record ViewportSprite(Guid TextureId, TextureData Texture, SpriteRegion Region);

/// <summary>Owns GPU resources only inside Avalonia's context callbacks.</summary>
[SuppressMessage("Design", "CA1001", Justification = "Avalonia owns the control context; GPU resources are released only in OnOpenGlDeinit while that context is current.")]
internal sealed class StudioViewport : OpenGlControlBase
{
    private readonly Dictionary<Guid, OpenGLTexture2D> _textures = [];
    private readonly DispatcherTimer _startup = new() { Interval = TimeSpan.FromSeconds(5) };
    private ImmutableArray<ViewportSprite> _frames = [];
    private OpenGLGraphicsDevice? _device;
    private OpenGLRenderer? _renderer;
    private GL? _gl;
    private bool _resetTextures;
    private bool _failed;
    private int _frameIndex;
    private double _zoom = 1;
    private WorldMapScene? _map;
    private IClientContentSource? _mapSource;
    private WorldMapTextureRenderer? _mapTextures;
    private int _logicalWidth = 512;
    private bool _needsRendererReset;
    private bool _mapReady;

    public StudioViewport()
    {
        Name = "PreviewViewport";
        _startup.Tick += (_, _) =>
        {
            _startup.Stop();
            if (_device is null)
            {
                Report("GPU preview unavailable. A desktop OpenGL 3.3 Core context with texture sharing is required. Use the Pixels tab to inspect image data.");
            }
        };
    }

    public event Action<string>? StatusChanged;
    internal ViewportSprite? CurrentSprite => _frames.IsEmpty ? null : _frames[_frameIndex];
    internal int FrameIndex => _frameIndex;

    public void SetFrames(ImmutableArray<ViewportSprite> frames)
    {
        Dispatcher.UIThread.VerifyAccess();
        _frames = frames;
        _map = null;
        _mapSource = null;
        _frameIndex = 0;
        _resetTextures = true;
        if (_needsRendererReset)
        {
            _failed = false;
        }
        RequestNextFrameRendering();
    }

    public void SetMap(WorldMapScene scene, IClientContentSource source)
    {
        Dispatcher.UIThread.VerifyAccess();
        _frames = [];
        _map = scene;
        scene.SetZoomPercent((int)Math.Round(_zoom * 100));
        _mapSource = source;
        _resetTextures = true;
        _mapReady = false;
        if (_needsRendererReset)
        {
            _failed = false;
        }
        RequestNextFrameRendering();
    }

    public void SetFrameIndex(int index)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (index < 0 || index >= _frames.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (_frameIndex != index)
        {
            _frameIndex = index;
            RequestNextFrameRendering();
        }
    }

    public void SetZoom(double zoom)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(zoom, 0.1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(zoom, 8);
        _zoom = zoom;
        _map?.SetZoomPercent((int)Math.Round(zoom * 100));
        RequestNextFrameRendering();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _startup.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _startup.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        _failed = false;
        _logicalWidth = 512;
        _needsRendererReset = false;
        try
        {
            _device = new OpenGLGraphicsDevice(gl.GetProcAddress);
            _gl = GL.GetApi(gl.GetProcAddress);
            _renderer = _device.CreateRenderer(new LogicalRenderSize(512, 512), 1, 1, colorFormat: RenderColorFormat.Rgba8);
            Report($"Shared preview renderer · {_device.Renderer} · {_device.Version}");
        }
        catch (Exception error) when (error is NotSupportedException or InvalidOperationException or ArgumentException)
        {
            _failed = true;
            Report("GPU preview failed: " + error.Message);
        }
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_failed || _renderer is null || _device is null || _gl is null)
        {
            return;
        }

        try
        {
            if (_resetTextures)
            {
                _mapTextures?.Dispose();
                _mapTextures = _mapSource is null ? null : new WorldMapTextureRenderer(_mapSource, _device, maximumTextures: 1024, maximumPixelBytes: 256 * 1024 * 1024);
                foreach (OpenGLTexture2D texture in _textures.Values)
                {
                    texture.Dispose();
                }

                _textures.Clear();
                _resetTextures = false;
            }

            double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            int width = Math.Max(1, (int)(Bounds.Width * scaling));
            int height = Math.Max(1, (int)(Bounds.Height * scaling));
            int logicalWidth = _map is null ? 512 : 1024;
            if (_logicalWidth != logicalWidth || _needsRendererReset)
            {
                _renderer.Dispose();
                _logicalWidth = logicalWidth;
                _renderer = _device.CreateRenderer(new LogicalRenderSize(logicalWidth, _map is null ? 512 : 768), width, height, colorFormat: RenderColorFormat.Rgba8);
                _needsRendererReset = false;
            }
            _renderer.ResizeHostFramebuffer(width, height);
            _renderer.BeginFrame();
            if (_map is not null && _mapTextures is not null)
            {
                if (_map.Prepare(_mapTextures, 1024, 768, unchecked((uint)Environment.TickCount), textureBudget: 4))
                {
                    _map.Draw(_mapTextures, _renderer);
                    if (!_mapReady)
                    {
                        _mapReady = true;
                        Report($"Client map {_map.Map.MapDataId} · shared puzzle/terrain renderer · {_device.Renderer}");
                    }
                }

                _renderer.EndFrame((uint)fb);
                CaptureIfRequested(width, height);
                RequestNextFrameRendering();
                return;
            }
            for (int y = 0; y < 512; y += 32)
            {
                for (int x = 0; x < 512; x += 32)
                {
                    byte shade = (x + y) % 64 == 0 ? (byte)48 : (byte)64;
                    _renderer.DrawSolidRectangle(x, y, 32, 32, new SpriteColor(shade, shade, shade, 255));
                }
            }

            if (CurrentSprite is { } sprite)
            {
                if (!_textures.TryGetValue(sprite.TextureId, out OpenGLTexture2D? texture))
                {
                    texture = _device.CreateTexture2D(sprite.Texture.Width, sprite.Texture.Height, sprite.Texture.Pixels.Span);
                    _textures.Add(sprite.TextureId, texture);
                }

                SpriteRegion region = sprite.Region;
                double scale = Math.Min(480d / region.Width, 480d / region.Height) * _zoom;
                _renderer.DrawSprite(texture, new SpriteSourceRectangle(region.X, region.Y, region.Width, region.Height),
                    256 - (int)(region.PivotX * scale), 256 - (int)(region.PivotY * scale),
                    Math.Max(1, (int)(region.Width * scale)), Math.Max(1, (int)(region.Height * scale)));
            }

            _renderer.EndFrame((uint)fb);
            CaptureIfRequested(width, height);
        }
        catch (Exception error) when (error is NotSupportedException or InvalidOperationException or ArgumentException or IOException or InvalidDataException)
        {
            _failed = true;
            _needsRendererReset = true;
            _capture?.TrySetException(error);
            _capture = null;
            Report("GPU preview failed: " + error.Message);
        }
    }

    private TaskCompletionSource<(int Width, int Height, byte[] Pixels)>? _capture;

    // Rendering validation reads the actual host framebuffer, after the engine presents into it.
    internal Task<(int Width, int Height, byte[] Pixels)> CaptureNextFrame()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_capture is not null)
        {
            throw new InvalidOperationException("A viewport capture is already pending.");
        }

        if (_failed)
        {
            throw new InvalidOperationException("The viewport graphics context failed.");
        }

        _capture = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RequestNextFrameRendering();
        return _capture.Task;
    }

    private unsafe void CaptureIfRequested(int width, int height)
    {
        if (_capture is null)
        {
            return;
        }

        TaskCompletionSource<(int Width, int Height, byte[] Pixels)> capture = _capture;
        _capture = null;
        if ((long)width * height * 4 > 64 * 1024 * 1024)
        {
            capture.TrySetException(new InvalidOperationException("Viewport capture exceeds 64 MiB."));
            return;
        }

        byte[] pixels = new byte[width * height * 4];
        fixed (byte* pointer = pixels)
        {
            _gl!.ReadPixels(0, 0, (uint)width, (uint)height, PixelFormat.Rgba, PixelType.UnsignedByte, pointer);
        }

        GLEnum error = _gl!.GetError();
        if (error != GLEnum.NoError)
        {
            capture.TrySetException(new InvalidOperationException($"Viewport capture reported OpenGL error {error}."));
        }
        else
        {
            capture.TrySetResult((width, height, pixels));
        }
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        try
        {
            foreach (OpenGLTexture2D texture in _textures.Values)
            {
                texture.Dispose();
            }

            _mapTextures?.Dispose();

            _renderer?.Dispose();
        }
        finally
        {
            _device?.Dispose();
            _gl?.Dispose();
            ClearContext();
        }
    }

    protected override void OnOpenGlLost()
    {
        // Context destruction releases GPU names. Never issue deletion calls on a lost context.
        ClearContext();
        Report("Graphics context lost. The next context will rebuild the preview from CPU resources.");
    }

    private void ClearContext()
    {
        _capture?.TrySetException(new InvalidOperationException("The viewport graphics context closed."));
        _capture = null;
        _textures.Clear();
        _mapTextures = null;
        _renderer = null;
        _device = null;
        _gl = null;
    }

    private void Report(string message) => StatusChanged?.Invoke(message);
}
