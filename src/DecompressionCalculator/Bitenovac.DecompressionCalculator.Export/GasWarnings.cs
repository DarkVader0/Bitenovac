using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Export;

/// <summary>
/// Provides the warnings about the gases of a dive, worded the same on screen, on the sheet and in the summary.
/// </summary>
public static class GasWarnings
{
    /// <summary>
    /// Returns a warning for each gas denser than <see cref="GasMath.DensityLimit"/> where it is breathed, then one for
    /// each switch that risks isobaric counterdiffusion.
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
            .Select(check =>
                $"{check.Gas} is {Formats.Number(check.Density, 1)} g/L at {Formats.Number(check.Depth)} {labels.Depth}, " +
                $"above {Formats.Number(GasMath.DensityLimit, 1)} g/L: breathing gets hard work. Add helium or plan shallower.");
        var counterdiffusion = result.Counterdiffusion
            .Select(row =>
                $"The switch from {row.From} to {row.To} at {Formats.Number(row.Depth)} {labels.Depth} adds more nitrogen than a fifth " +
                "of the helium it drops, which risks isobaric counterdiffusion.");
        return [.. density, .. counterdiffusion];
    }
}
