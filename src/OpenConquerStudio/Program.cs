using Avalonia;
using Avalonia.OpenGL;

namespace OpenConquerStudio;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 1)
        {
            Console.Error.WriteLine("Usage: OpenConquerStudio [project-directory]");
            return 2;
        }

        return Configure().StartWithClassicDesktopLifetime(args);
    }

    internal static AppBuilder Configure() => DesktopGraphics.Configure(AppBuilder.Configure<StudioApp>());
}

internal static class DesktopGraphics
{
    internal static AppBuilder Configure(AppBuilder builder, bool allowSoftwareGpu = false)
    {
        X11PlatformOptions linux = new()
        {
            RenderingMode = [X11RenderingMode.Glx, X11RenderingMode.Software],
            GlProfiles = [new GlVersion(GlProfileType.OpenGL, 4, 1), new GlVersion(GlProfileType.OpenGL, 3, 3)],
        };
        if (allowSoftwareGpu)
        {
            // Mesa provides real OpenGL readback in virtualized rendering-validation hosts.
            linux.GlxRendererBlacklist = [];
        }

        return builder.UsePlatformDetect()
            .With(new Win32PlatformOptions
            {
                RenderingMode = [Win32RenderingMode.Wgl, Win32RenderingMode.Software],
                WglProfiles = [new GlVersion(GlProfileType.OpenGL, 4, 1), new GlVersion(GlProfileType.OpenGL, 3, 3)],
            })
            .With(linux)
            .With(new AvaloniaNativePlatformOptions
            {
                RenderingMode = [AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Software],
            });
    }
}
