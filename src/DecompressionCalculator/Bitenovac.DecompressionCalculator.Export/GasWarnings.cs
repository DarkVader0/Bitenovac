using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Export;

/// <summary>
/// Provides the warnings about the gases of a dive, worded the same on screen, on the sheet and in the summary.
/// </summary>
public static class GasWarnings
{
    /// <summary>
    /// Returns a warning for the densest gas where it is breathed, when it is denser than the limit in the planner settings,
    /// then one for each switch that risks isobaric counterdiffusion.
    /// </summary>
    /// <param name="result">The dive.</param>
    /// <param name="units">The units the dive is shown in.</param>
    /// <returns>The warnings, empty when there is nothing to warn about.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<string> For(DiveResult result, UnitSystem units)
    {
        ArgumentNullException.ThrowIfNull(result);

        var labels = UnitLabels.For(units);
        var density = result.GasChecks
            .Where(static check => check.IsTooDense)
            .OrderByDescending(static check => check.Density)
            .Take(1)
            .Select(check => Localized.Format(ExportText.DensityWarning, check.Gas, Formats.Number(check.Density, 1),
                Formats.Number(check.Depth), labels.Depth, Formats.Number(check.DensityLimit, 1)));
        var counterdiffusion = result.Counterdiffusion
            .Select(row => Localized.Format(ExportText.CounterdiffusionWarning, row.From, row.To, Formats.Number(row.Depth), labels.Depth));
        return [.. density, .. counterdiffusion];
    }
}
