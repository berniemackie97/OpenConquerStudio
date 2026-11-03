using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using OpenConquer.Content;
using OpenConquer.Content.Mesh;
using OpenConquer.Rendering.World;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio;

/// <summary>Owns the editor's preview surfaces and playback controls, separate from project mutations.</summary>
internal sealed class AssetPreview : Grid, IDisposable
{
    private readonly StudioViewport _viewport = new();
    private readonly AnimationTransport _transport;
    private readonly Image _pixels = new() { Stretch = Stretch.Uniform, Margin = new Thickness(24), Name = "TexturePreview" };
    private WriteableBitmap? _bitmap;
    private bool _modelActive;
    public bool IsWorldModelActive
    {
        get; private set;
    }
    public event Action<int>? ModelFrameChanged;
    private readonly TextBlock _zoomLabel = new() { Text = "Zoom (1 = fit)" };

    public AssetPreview()
    {
        _transport = new AnimationTransport(frame =>
        {
            if (_modelActive)
            {
                _viewport.SetModelFrame(frame);
                ModelFrameChanged?.Invoke(frame);
            }
            else
            {
                _viewport.SetFrameIndex(frame);
            }
        });
        TextBlock status = new()
        {
            Text = "GPU preview requires desktop OpenGL 3.3 Core.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8)
        };
        _viewport.StatusChanged += message => status.Text = message;
        Slider zoom = new()
        {
            Minimum = 0.1,
            Maximum = 8,
            Value = 1,
            Name = "ViewportZoom"
        };
        zoom.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) { _viewport.SetZoom(zoom.Value); } };
        Grid preview = new()
        {
            RowDefinitions = new RowDefinitions("*,Auto,Auto,Auto"),
            RowSpacing = 6
        };
        preview.Children.Add(_viewport);
        preview.Children.Add(new StackPanel { Spacing = 4, Children = { _zoomLabel, zoom }, [RowProperty] = 1 });
        SetRow(_transport, 2);
        preview.Children.Add(_transport);
        SetRow(status, 3);
        preview.Children.Add(status);
        Children.Add(new TabControl
        {
            ItemsSource = new TabItem[]
            {
                new() { Header = "Preview", Content = preview },
                new() { Header = "Pixels (source texture)", Content = _pixels },
            },
        });
        _pixels.SetValue(AutomationProperties.NameProperty, "Source texture pixels");
        _viewport.SetValue(AutomationProperties.NameProperty, "Rendered asset preview");
    }

    public void Load(ImmutableArray<ViewportSprite> frames, SpriteAnimation? animation)
    {
        _modelActive = false;
        _zoomLabel.Text = "Zoom (1 = fit)";
        TextureData texture = frames[0].Texture;
        WriteableBitmap bitmap = new(new PixelSize(texture.Width, texture.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        try
        {
            byte[] pixels = texture.Pixels.ToArray();
            using ILockedFramebuffer framebuffer = bitmap.Lock();
            for (int y = 0; y < texture.Height; y++)
            {
                Marshal.Copy(pixels, y * texture.Width * 4, framebuffer.Address + y * framebuffer.RowBytes, texture.Width * 4);
            }
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }

        _pixels.Source = bitmap;
        _bitmap?.Dispose();
        _bitmap = bitmap;
        _viewport.SetFrames(frames);
        _transport.Load(animation);
    }

    public void LoadModel(C3ModelPreview model)
    {
        Clear();
        _modelActive = true;
        _zoomLabel.Text = "Zoom (1 = fit)";
        _viewport.SetModel(model);
        _transport.LoadModel(checked((int)model.FrameCount), model.Clip?.FrameMilliseconds ?? 66, model.Clip?.Repeat ?? true);
        Children.OfType<TabControl>().Single().SelectedIndex = 0;
    }

    public void ModelView(bool bones, float rotation) => _viewport.SetModelView(bones, rotation);

    public void SelectFrame(int index) => _transport.SelectFrame(index);
    public void Pause() => _transport.Pause();

    public void LoadMap(WorldMapScene scene, IClientContentSource source, C3ModelPreview? model = null)
    {
        Clear();
        _zoomLabel.Text = "Zoom (1 = 100%)";
        _viewport.SetMap(scene, source);
        if (model is not null)
        {
            _modelActive = true;
            IsWorldModelActive = true;
            _viewport.SetWorldModel(model);
            _transport.LoadModel(checked((int)model.FrameCount), model.Clip?.FrameMilliseconds ?? 66, model.Clip?.Repeat ?? true);
        }
        Children.OfType<TabControl>().Single().SelectedIndex = 0;
    }

    public void RefreshMap() => _viewport.RequestNextFrameRendering();

    public void Clear()
    {
        _transport.Load(null);
        _modelActive = false;
        IsWorldModelActive = false;
        _viewport.SetFrames([]);
        _pixels.Source = null;
        _bitmap?.Dispose();
        _bitmap = null;
    }

    public void Dispose() => Clear();
}
