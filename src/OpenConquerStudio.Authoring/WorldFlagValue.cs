using System.Globalization;

namespace OpenConquerStudio.Authoring;

public static class WorldFlagValue
{
    public static ulong Parse(string? text)
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
