using System.Globalization;

namespace OpenConquer.Content.Configuration;

public sealed class GameSetupConfiguration
{
    public const string RelativePath = "ini/GameSetUp.ini";

    private const int MaximumFileLength = 64 * 1024;
    private const string ScreenModeSectionName = "ScreenMode";
    private const string ScreenModeKeyName = "ScreenModeRecord";

    private GameSetupConfiguration(int screenMode)
    {
        ScreenMode = screenMode;
    }

    public int ScreenMode
    {
        get;
    }

    // Screen modes 0-1 share the 800x600 logical canvas; modes 2-3 share 1024x768.
    public int LogicalWidthPixels => ScreenMode is 0 or 1 ? 800 : 1024;

    public int LogicalHeightPixels => ScreenMode is 0 or 1 ? 600 : 768;

    public static GameSetupConfiguration Load(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        IniDocument document = IniDocument.LoadRequired(contentSource, RelativePath, MaximumFileLength);

        if (!document.TryGetValue(ScreenModeSectionName, ScreenModeKeyName, out string? value))
        {
            throw new InvalidDataException($"'{RelativePath}' does not define [{ScreenModeSectionName}] {ScreenModeKeyName}.");
        }

        if (!int.TryParse(value, style: NumberStyles.Integer, CultureInfo.InvariantCulture, out int screenMode) || screenMode is < 0 or > 3)
        {
            throw new InvalidDataException($"'{RelativePath}' contains an invalid [{ScreenModeSectionName}] {ScreenModeKeyName} value '{value}'. Expected an integer from 0 through 3.");
        }

        return new GameSetupConfiguration(screenMode);
    }
}
