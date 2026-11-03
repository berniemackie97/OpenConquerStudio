using System.Globalization;
using Avalonia.Controls;

namespace OpenConquerStudio;

internal static class EditorInput
{
    internal static uint UnsignedInteger(NumericUpDown input, string label)
    {
        if (!uint.TryParse(input.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out uint value) || value < input.Minimum || value > input.Maximum)
        {
            throw new InvalidDataException($"Enter a whole number from {input.Minimum} to {input.Maximum} for {label}.");
        }
        return value;
    }

    internal static int Integer(NumericUpDown input, string label)
    {
        if (!int.TryParse(input.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int value) ||
            value < input.Minimum || value > input.Maximum)
        {
            throw new InvalidDataException($"Enter a whole number from {input.Minimum} to {input.Maximum} for {label}.");
        }
        return value;
    }
}
