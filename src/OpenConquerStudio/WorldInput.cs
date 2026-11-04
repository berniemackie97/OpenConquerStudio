using System.Globalization;

namespace OpenConquerStudio;

internal static class WorldInput
{
    internal static uint ReadMapId(string? text)
    {
        if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint mapId) || mapId == 0)
        {
            throw new InvalidDataException("Enter a nonzero unsigned 32-bit world MapId.");
        }

        return mapId;
    }

}
