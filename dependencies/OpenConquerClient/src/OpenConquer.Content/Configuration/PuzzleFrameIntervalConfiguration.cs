using System.Globalization;

namespace OpenConquer.Content.Configuration;

public static class PuzzleFrameIntervalConfiguration
{
    public static uint Load(IClientContentSource source)
    {
        IniDocument info = IniDocument.LoadRequired(source, "ini/info.ini", 64 * 1024);
        if (!info.TryGetValue("PuzzleBmp", "FrameIntervalMilliSec", out string? value) ||
            !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int interval))
        {
            return 1000;
        }

        return (uint)Math.Max(interval, 1);
    }
}
