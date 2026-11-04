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

    internal static ulong ReadFlags(string? text)
    {
        string value = text?.Trim() ?? "";
        bool hex = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        string digits = hex ? value[2..] : value;
        NumberStyles styles = hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None;
        if (digits.Length == 0 || !ulong.TryParse(digits, styles, CultureInfo.InvariantCulture, out ulong flags))
        {
            throw new InvalidDataException("Enter explicit 64-bit map flags (unsigned decimal or 0x-prefixed hexadecimal).");
        }

        return flags;
    }
}
