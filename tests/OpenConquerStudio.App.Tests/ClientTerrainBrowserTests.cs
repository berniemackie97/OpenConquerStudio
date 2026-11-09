using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenConquerStudio.ConquerImport;

namespace OpenConquerStudio.App.Tests;

public sealed partial class StudioWindowTests
{
    [Fact]
    public Task SourceMapOffersExplicitTerrainOnlyExtractionAndRefresh() => s_session.Value.Dispatch(async () =>
    {
        int previews = 0;
        int imports = 0;
        int refreshes = 0;
        ClientContentPanel panel = new(
            (entry, _) =>
            {
                Assert.Equal("map", entry.Kind);
                previews++;
                return Task.CompletedTask;
            },
            (entry, read, refresh) =>
            {
                Assert.Equal(9100u, entry.MapDataId);
                Assert.Null(read().Section);
                if (refresh)
                {
                    refreshes++;
                }
                else
                {
                    imports++;
                }
                return Task.CompletedTask;
            },
            (_, _) => { }, () => { });
        Window window = new()
        {
            Content = new StackPanel { Children = { panel, panel.Inspector } }
        };
        window.Show();
        try
        {
            panel.Load([new ClientAssetEntry("map", "maps/ground.dmap", 9100)], "content");
            Find<ListBox>(window, "ClientContentBrowser").SelectedIndex = 0;
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { });

            Button extract = Find<Button>(window, "ImportClientAsset");
            Button refresh = Find<Button>(window, "ReimportClientAsset");
            Assert.True(extract.IsVisible);
            Assert.True(refresh.IsVisible);
            Assert.Equal("Extract editable terrain grid", extract.Content);
            Assert.Equal("Refresh selected authored terrain", refresh.Content);
            Assert.True(previews > 0);

            extract.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(1, imports);
            Assert.Equal(1, refreshes);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
