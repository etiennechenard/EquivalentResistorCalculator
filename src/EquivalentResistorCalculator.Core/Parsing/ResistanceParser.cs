using System.Globalization;

namespace EquivalentResistorCalculator.Core.Parsing;

public static class ResistanceParser
{
    public static bool TryParse(string? input, out double ohms)
    {
        ohms = 0;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        string text = input.Trim().ToUpperInvariant();
        double multiplier = 1;

        if (text.EndsWith("MEG", StringComparison.Ordinal))
        {
            multiplier = 1_000_000;
            text = text[..^3];
        }
        else if (text.EndsWith("M", StringComparison.Ordinal))
        {
            multiplier = 1_000_000;
            text = text[..^1];
        }
        else if (text.EndsWith("K", StringComparison.Ordinal))
        {
            multiplier = 1_000;
            text = text[..^1];
        }
        else if (text.EndsWith("R", StringComparison.Ordinal))
        {
            multiplier = 1;
            text = text[..^1];
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            ohms = value * multiplier;
            return true;
        }

        return false;
    }

    public static string Format(double ohms)
    {
        if (ohms >= 1_000_000)
            return (ohms / 1_000_000).ToString("G4", CultureInfo.InvariantCulture) + "M";
        if (ohms >= 1_000)
            return (ohms / 1_000).ToString("G4", CultureInfo.InvariantCulture) + "K";
        return ohms.ToString("G4", CultureInfo.InvariantCulture);
    }
}
