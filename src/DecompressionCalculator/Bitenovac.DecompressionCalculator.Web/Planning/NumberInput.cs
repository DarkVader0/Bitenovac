using System.Globalization;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Converts between an entered number and the text of a number input; an empty or unreadable entry is NaN.
/// </summary>
public static class NumberInput
{
    public static string Text(double value) => double.IsFinite(value) ? value.ToString("R", CultureInfo.InvariantCulture) : "";

    public static double Parse(object? text) =>
        double.TryParse(text as string, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN;
}
