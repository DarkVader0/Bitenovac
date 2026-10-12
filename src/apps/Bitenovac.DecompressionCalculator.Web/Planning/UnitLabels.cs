using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Provides the unit suffixes shown next to values entered in a plan's unit system.
/// </summary>
public sealed record UnitLabels(string Depth, string Rate, string Pressure, string GasVolume, string GasRate, string CylinderSize)
{
    public static UnitLabels Metric { get; } = new("m", "m/min", "bar", "L", "L/min", "L");

    public static UnitLabels Imperial { get; } = new("ft", "ft/min", "psi", "cu ft", "cu ft/min", "cu ft");

    public static UnitLabels For(UnitSystem units) => units == UnitSystem.Imperial ? Imperial : Metric;
}
