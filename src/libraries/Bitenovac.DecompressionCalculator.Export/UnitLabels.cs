using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Export;

/// <summary>
/// Represents the unit suffixes shown next to values in a plan's unit system.
/// </summary>
/// <param name="Depth">The depth unit: <c>m</c> or <c>ft</c>.</param>
/// <param name="Rate">The vertical speed unit: <c>m/min</c> or <c>ft/min</c>.</param>
/// <param name="Pressure">The cylinder pressure unit: <c>bar</c> or <c>psi</c>.</param>
/// <param name="GasVolume">The gas volume unit: <c>L</c> or <c>cu ft</c>.</param>
/// <param name="GasRate">The breathing rate unit: <c>L/min</c> or <c>cu ft/min</c>.</param>
/// <param name="CylinderSize">The cylinder size unit: water <c>L</c> or rated <c>cu ft</c>.</param>
public sealed record UnitLabels(string Depth, string Rate, string Pressure, string GasVolume, string GasRate, string CylinderSize)
{
    /// <summary>
    /// Gets the metric suffixes.
    /// </summary>
    public static UnitLabels Metric { get; } = new("m", "m/min", "bar", "L", "L/min", "L");

    /// <summary>
    /// Gets the imperial suffixes.
    /// </summary>
    public static UnitLabels Imperial { get; } = new("ft", "ft/min", "psi", "cu ft", "cu ft/min", "cu ft");

    /// <summary>
    /// Returns the suffixes of a unit system.
    /// </summary>
    /// <param name="units">The unit system.</param>
    /// <returns>The imperial suffixes for <see cref="UnitSystem.Imperial"/>, and the metric ones otherwise.</returns>
    public static UnitLabels For(UnitSystem units) => units == UnitSystem.Imperial ? Imperial : Metric;
}
