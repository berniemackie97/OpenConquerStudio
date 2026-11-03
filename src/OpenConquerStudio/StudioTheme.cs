using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace OpenConquerStudio;

internal static class StudioTheme
{
    internal static void Apply(Application application)
    {
        application.Name = "OpenConquerStudio";
        application.RequestedThemeVariant = ThemeVariant.Dark;
        application.Styles.Add(new Style(s => s.OfType<Window>())
        {
            Setters = { new Setter(TemplatedControl.FontSizeProperty, 13d), new Setter(TemplatedControl.BackgroundProperty, new SolidColorBrush(Color.Parse("#20242B"))) }
        });
        application.Styles.Add(new Style(s => s.OfType<Button>())
        {
            Setters = { new Setter(Layoutable.MinHeightProperty, 30d), new Setter(TemplatedControl.PaddingProperty, new Thickness(10, 5)) }
        });
        application.Styles.Add(new Style(s => s.OfType<TabItem>())
        {
            Setters = { new Setter(TemplatedControl.FontSizeProperty, 13d), new Setter(TemplatedControl.PaddingProperty, new Thickness(10, 6)) }
        });
    }
}
