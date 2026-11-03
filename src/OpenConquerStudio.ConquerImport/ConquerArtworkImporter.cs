using System.Collections.Immutable;
using OpenConquer.Content.Images;
using OpenConquer.Content.Login;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio.ConquerImport;

public sealed record ArtworkSource(string Key, string Name);

/// <summary>Imports supported client image resources through their existing integrity-checked loaders.</summary>
public static class ConquerArtworkImporter
{
    private static readonly ImmutableArray<(ArtworkSource Source, Func<RgbaImage> Load)> s_sources =
    [
        (new("conquer/login/account/row", "Account row"), () => LoginAccountArtwork.LoadRow(false)),
        (new("conquer/login/account/row-highlighted", "Account row highlighted"), () => LoginAccountArtwork.LoadRow(true)),
        (new("conquer/login/account/save-dialog", "Save account dialog image"), () => LoginAccountArtwork.LoadDialog(LoginAccountDialog.Save)),
        (new("conquer/login/account/clear-dialog", "Clear accounts dialog image"), () => LoginAccountArtwork.LoadDialog(LoginAccountDialog.Clear)),
        (new("conquer/login/account/ok-over", "Account OK hover"), () => LoginAccountArtwork.LoadDialogButton(LoginDialogButton.Ok, LoginButtonState.Over)),
        (new("conquer/login/account/ok-down", "Account OK pressed"), () => LoginAccountArtwork.LoadDialogButton(LoginDialogButton.Ok, LoginButtonState.Down)),
        (new("conquer/login/account/cancel-over", "Account cancel hover"), () => LoginAccountArtwork.LoadDialogButton(LoginDialogButton.Cancel, LoginButtonState.Over)),
        (new("conquer/login/account/cancel-down", "Account cancel pressed"), () => LoginAccountArtwork.LoadDialogButton(LoginDialogButton.Cancel, LoginButtonState.Down)),
    ];

    public static ImmutableArray<ArtworkSource> Sources { get; } = s_sources.Select(entry => entry.Source).ToImmutableArray();

    public static Guid Import(StudioProject project, string key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        cancellationToken.ThrowIfCancellationRequested();
        foreach ((ArtworkSource source, Func<RgbaImage> load) in s_sources)
        {
            if (!string.Equals(source.Key, key, StringComparison.Ordinal))
            {
                continue;
            }

            RgbaImage image = load();
            cancellationToken.ThrowIfCancellationRequested();
            return project.ImportImage(source.Name, new TextureData(image.Width, image.Height, image.Pixels.Span), source.Key);
        }

        throw new InvalidDataException("The client artwork key is unsupported. Select an entry from the artwork catalog.");
    }
}
