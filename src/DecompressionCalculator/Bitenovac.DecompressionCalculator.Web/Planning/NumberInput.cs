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

    /// <returns>Whether the text reads as the value, so the text can stay as the user typed it, such as <c>1.</c> on the way to <c>1.5</c>.</returns>
    public static bool Matches(string text, double value)
    {
        var parsed = Parse(text);
        return double.IsNaN(parsed) ? double.IsNaN(value) : parsed == value;
    }
}
