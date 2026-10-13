using System.Globalization;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Converts between a cylinder's oxygen and helium percentages and the text of its gas field.
/// </summary>
/// <remarks>
/// The field shows <c>O2/He</c>, such as <c>21/0</c> or <c>18/45</c>. It also reads the names divers
/// write: <c>Air</c>, <c>O2</c>, <c>NX32</c> or <c>EAN32</c>, <c>TX18/45</c>, and a bare oxygen
/// percentage such as <c>32</c>, which is nitrox.
/// </remarks>
public static class GasText
{
    public static string Format(double oxygenPercent, double heliumPercent) =>
        double.IsFinite(oxygenPercent) && double.IsFinite(heliumPercent)
            ? string.Create(CultureInfo.InvariantCulture, $"{oxygenPercent:0.#}/{heliumPercent:0.#}")
            : "";

    /// <returns>The percentages, or <see langword="null"/> when the text is not a gas.</returns>
    public static (double Oxygen, double Helium)? Parse(string? text)
    {
        var gas = (text ?? "").Trim().ToUpperInvariant().Replace(" ", "", StringComparison.Ordinal);

        return gas switch
        {
            "AIR" => (21, 0),
            "O2" or "OXYGEN" => (100, 0),
            _ when gas.StartsWith("EAN", StringComparison.Ordinal) || gas.StartsWith("NX", StringComparison.Ordinal)
                || gas.StartsWith("TX", StringComparison.Ordinal) => Mix(gas.TrimStart('E', 'A', 'N', 'X', 'T')),
            _ => Mix(gas)
        };
    }

    private static (double Oxygen, double Helium)? Mix(string text)
    {
        var parts = text.Split('/');
        return parts switch
        {
            [var oxygen] when Number(oxygen) is { } o2 => (o2, 0),
            [var oxygen, var helium] when Number(oxygen) is { } o2 && Number(helium) is { } he => (o2, he),
            _ => null
        };
    }

    private static double? Number(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
}
