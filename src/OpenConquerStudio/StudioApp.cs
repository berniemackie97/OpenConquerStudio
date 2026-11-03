using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace OpenConquerStudio;

public sealed class StudioApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        StudioTheme.Apply(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            string? root = StudioStartupProject.Resolve(desktop.Args is [string path] ? path : null,
                Environment.CurrentDirectory, AppContext.BaseDirectory);
            desktop.MainWindow = new StudioWindow(root);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
