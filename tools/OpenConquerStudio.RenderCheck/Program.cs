using System.Collections.Immutable;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using OpenConquer.Rendering.Presentation;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.ConquerImport;
using OpenConquerStudio.ConquerRendering;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio.RenderCheck;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
        Trace.AutoFlush = true;
        if (args.Length > 1)
        {
            throw new ArgumentException("Use an optional client repository directory argument.", nameof(args));
        }
        string? studio = AppContext.BaseDirectory;
        while (studio is not null && !File.Exists(Path.Combine(studio, "OpenConquerStudio.slnx")))
        {
            studio = Path.GetDirectoryName(studio);
        }
        string repository = args.Length == 1 ? Path.GetFullPath(args[0])
            : Path.Combine(studio ?? throw new DirectoryNotFoundException("Specify a client repository for rendering validation."),
                "dependencies", "OpenConquerClient");
        if (!ClientProjectConnection.IsClientRepository(repository))
        {
            throw new DirectoryNotFoundException("Select a client source checkout, or initialize the pinned client dependency.");
        }

        string parent = OperatingSystem.IsMacOS() ? "/private/tmp" : Path.GetTempPath();
        string root = Path.Combine(parent, "studio-client-render-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using (StudioProject project = StudioProject.Create(root, "Client asset validation"))
            {
                ClientProjectConnection connection = ClientProjectConnection.Attach(project, repository,
                    Path.Combine(repository, "content", "retail-5517", "payload"));
                project.ImportAnimation("Action states", connection.ReadAnimation("ani/control.ani", "Action_Act0Btn"), 100);
                project.Save();
                Directory.CreateDirectory(Path.Combine(root, "Builds"));
                StudioCompiler.Compile(project, Path.Combine(root, "Builds", "verified"));
                RenderCheckApp.Connection = connection;
                var map = connection.ReadMap(1010);
                RenderCheckApp.Map = WorldMapScene.Load(connection.Source, map, (ushort)(map.CellGrid.Width / 2),
                    (ushort)(map.CellGrid.Height / 2), unchecked((uint)Environment.TickCount));
            }

            RenderCheckApp.Package = ContentPackage.Load(Path.Combine(root, "Builds", "verified"));
            return DesktopGraphics.Configure(AppBuilder.Configure<RenderCheckApp>(), allowSoftwareGpu: true).LogToTrace().StartWithClassicDesktopLifetime([]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

public sealed class RenderCheckApp : Application
{
    internal static ContentPackage Package { get; set; } = null!;
    internal static ClientProjectConnection Connection { get; set; } = null!;
    internal static WorldMapScene Map { get; set; } = null!;

    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            StudioViewport viewport = new();
            viewport.StatusChanged += Console.WriteLine;
            Window window = new()
            {
                Title = "OpenConquerStudio client rendering validation",
                Width = 640,
                Height = 480,
                Content = viewport
            };
            desktop.MainWindow = window;
            window.Opened += async (_, _) =>
            {
                try
                {
                    SpriteAnimation animation = Package.Sprites.Animations.Values.Single();
                    ImmutableArray<ViewportSprite> frames = [.. animation.Frames.Select(frame =>
                    {
                        SpriteAtlas atlas = Package.Sprites.Atlases[frame.AtlasId];
                        return new ViewportSprite(atlas.TextureId, Package.Sprites.Textures[atlas.TextureId], atlas.Find(frame.SpriteId));
                    })];
                    viewport.SetFrames(frames);
                    var first = await viewport.CaptureNextFrame().WaitAsync(TimeSpan.FromSeconds(20));
                    ExpectSourcePixel(first, frames[0]);
                    viewport.SetFrameIndex(animation.SampleTicks(100));
                    var second = await viewport.CaptureNextFrame().WaitAsync(TimeSpan.FromSeconds(10));
                    ExpectSourcePixel(second, frames[1]);
                    window.Width = 800;
                    window.Height = 600;
                    await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                    var resized = await viewport.CaptureNextFrame().WaitAsync(TimeSpan.FromSeconds(10));
                    ExpectSourcePixel(resized, frames[1]);
                    await LoadMap(viewport);
                    var map = await viewport.CaptureNextFrame().WaitAsync(TimeSpan.FromSeconds(10));
                    RequireMapPixels(map.Pixels);
                    Map.SetCameraTile(44, 88);
                    var moved = await viewport.CaptureNextFrame().WaitAsync(TimeSpan.FromSeconds(10));
                    if (map.Pixels.AsSpan().SequenceEqual(moved.Pixels))
                    {
                        throw new InvalidDataException("Moving the actual map camera did not change its framebuffer.");
                    }

                    StudioViewport replacement = new();
                    window.Content = replacement;
                    await LoadMap(replacement);
                    var recreated = await replacement.CaptureNextFrame().WaitAsync(TimeSpan.FromSeconds(20));
                    RequireMapPixels(recreated.Pixels);
                    Console.WriteLine("PASS: actual Control.ani frames imported, compiled, loaded, and rendered; actual map 1010 puzzles/terrain rendered; camera, resize, and context recreation validated.");
                    desktop.Shutdown(0);
                }
                catch (Exception error)
                {
                    // This executable is a platform gate; failures are reported and return a failing exit code.
                    Console.Error.WriteLine(error);
                    desktop.Shutdown(1);
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task LoadMap(StudioViewport viewport)
    {
        TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        void Status(string message)
        {
            Console.WriteLine(message);
            if (message.StartsWith("Client map ", StringComparison.Ordinal))
            {
                ready.TrySetResult();
            }
            else if (message.Contains("failed", StringComparison.OrdinalIgnoreCase))
            {
                ready.TrySetException(new InvalidOperationException(message));
            }
        }
        viewport.StatusChanged += Status;
        try
        {
            viewport.SetMap(Map, Connection.Source);
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally
        {
            viewport.StatusChanged -= Status;
        }
    }

    private static void RequireMapPixels(byte[] pixels)
    {
        HashSet<int> colors = [];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            colors.Add(pixels[i] << 16 | pixels[i + 1] << 8 | pixels[i + 2]);
        }
        if (colors.Count < 100)
        {
            throw new InvalidDataException("The actual map framebuffer contains insufficient image detail.");
        }
    }

    private static void ExpectSourcePixel((int Width, int Height, byte[] Pixels) capture, ViewportSprite frame)
    {
        SpriteRegion region = frame.Region;
        ReadOnlySpan<byte> source = frame.Texture.Pixels.Span;
        double scale = Math.Min(480d / region.Width, 480d / region.Height);
        int drawWidth = Math.Max(1, (int)(region.Width * scale));
        int drawHeight = Math.Max(1, (int)(region.Height * scale));
        PresentationViewport host = PresentationViewport.Compute(new LogicalRenderSize(512, 512), capture.Width, capture.Height, PresentationPolicy.Fit);
        for (int y = 1; y < region.Height - 1; y++)
        {
            for (int x = 1; x < region.Width - 1; x++)
            {
                int index = ((region.Y + y) * frame.Texture.Width + region.X + x) * 4;
                ReadOnlySpan<byte> pixel = source.Slice(index, 4);
                if (pixel[3] != 255 || !pixel.SequenceEqual(source.Slice(index - 4, 4)) || !pixel.SequenceEqual(source.Slice(index + 4, 4)))
                {
                    continue;
                }
                double logicalX = 256 - (int)(region.PivotX * scale) + (x + 0.5) * drawWidth / region.Width;
                double logicalY = 256 - (int)(region.PivotY * scale) + (y + 0.5) * drawHeight / region.Height;
                int targetX = host.OffsetX + (int)(logicalX * host.Width / 512);
                int targetY = host.OffsetY + (int)((512 - logicalY) * host.Height / 512);
                int target = (targetY * capture.Width + targetX) * 4;
                if (!pixel.SequenceEqual(capture.Pixels.AsSpan(target, 4)))
                {
                    throw new InvalidDataException($"Rendered client image '{region.Name}' differs from its opaque source pixels.");
                }
                return;
            }
        }
        throw new InvalidDataException("The client image fixture has no opaque pixel suitable for rendering validation.");
    }
}
