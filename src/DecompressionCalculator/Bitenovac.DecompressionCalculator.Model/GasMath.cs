using Bitenovac.DecompressionAlgorithms.Core.Calculations;
using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Represents the richest mix a dive can breathe within an oxygen limit and a narcotic limit.
/// </summary>
/// <param name="PercentO2">The oxygen content, in percent, rounded down.</param>
/// <param name="PercentHe">The helium content, in percent, rounded up.</param>
public sealed record BestMix(int PercentO2, int PercentHe);

/// <summary>
/// Provides the gas calculations a diver plans with: maximum operating depth, equivalent narcotic depth, gas density
/// and the best mix for a depth, in the units of the plan and the environment of its settings.
/// </summary>
public static class GasMath
{
    /// <summary>
    /// The density, in grams per liter, above which a gas is warned about; above it breathing work rises steeply.
    /// </summary>
    public const double DefaultDensityLimit = 5.2;

    /// <summary>
    /// The density of oxygen, in grams per liter, at 0 °C and one standard atmosphere.
    /// </summary>
    public const double OxygenDensity = 1.429;

    /// <summary>
    /// The density of nitrogen, in grams per liter, at 0 °C and one standard atmosphere.
    /// </summary>
    public const double NitrogenDensity = 1.251;

    /// <summary>
    /// The density of helium, in grams per liter, at 0 °C and one standard atmosphere.
    /// </summary>
    public const double HeliumDensity = 0.1786;

    /// <summary>
    /// Returns the deepest depth at which a gas stays within an oxygen limit.
    /// </summary>
    /// <param name="percentO2">The oxygen content, in percent.</param>
    /// <param name="maxPo2">The oxygen partial pressure limit, in bar.</param>
    /// <param name="settings">The planner settings giving the environment and the depth model.</param>
    /// <param name="units">The units of the result.</param>
    /// <returns>The maximum operating depth, in meters or feet.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="percentO2"/> is not greater than 0 and at most 100, or <paramref name="maxPo2"/> is not greater than 0.
    /// </exception>
    public static double MaximumOperatingDepth(double percentO2, double maxPo2, PlannerSettings settings, UnitSystem units)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPo2);

        var meters = GasSelector.MaxOperatingDepthMeter(GasMixture.FromPercent(percentO2, 0), Pressure.FromBar(maxPo2),
            PlanMapper.ToSettings(settings, units));
        return UnitConversion.FromDepth(Depth.FromMeter(meters), units);
    }

    /// <summary>
    /// Returns the depth at which air is as narcotic as a gas is at a depth.
    /// </summary>
    /// <param name="percentO2">The oxygen content, in percent.</param>
    /// <param name="percentHe">The helium content, in percent.</param>
    /// <param name="depth">The depth the gas is breathed at, in meters or feet.</param>
    /// <param name="settings">The planner settings giving the environment and whether oxygen counts as narcotic.</param>
    /// <param name="units">The units of the depth and the result.</param>
    /// <returns>The equivalent narcotic depth, in meters or feet; never below the surface.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The gas is not a valid mix, or <paramref name="depth"/> is negative.</exception>
    public static double EquivalentNarcoticDepth(double percentO2, double percentHe, double depth, PlannerSettings settings, UnitSystem units)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfNegative(depth);

        var gas = GasMixture.FromPercent(percentO2, percentHe);
        var environment = PlanMapper.ToSettings(settings, units);
        return EquivalentNarcoticDepth(gas.FractionO2, gas.FractionN2, AmbientConditions.PressureAtDepth(environment, UnitConversion.ToDepth(depth, units)),
            environment, settings.OxygenNarcotic, units);
    }

    /// <summary>
    /// Returns the density of a gas breathed at a depth.
    /// </summary>
    /// <param name="percentO2">The oxygen content, in percent.</param>
    /// <param name="percentHe">The helium content, in percent.</param>
    /// <param name="depth">The depth the gas is breathed at, in meters or feet.</param>
    /// <param name="settings">The planner settings giving the environment.</param>
    /// <param name="units">The units of the depth.</param>
    /// <returns>The density, in grams per liter.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The gas is not a valid mix, or <paramref name="depth"/> is negative.</exception>
    /// <remarks>
    /// Each gas's density at 0 °C and one standard atmosphere, weighted by its fraction and scaled by the ambient pressure,
    /// as dive planners commonly state it.
    /// </remarks>
    public static double Density(double percentO2, double percentHe, double depth, PlannerSettings settings, UnitSystem units)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfNegative(depth);

        var gas = GasMixture.FromPercent(percentO2, percentHe);
        var ambient = AmbientConditions.PressureAtDepth(PlanMapper.ToSettings(settings, units), UnitConversion.ToDepth(depth, units));
        return Density(gas.FractionO2, gas.FractionN2, gas.FractionHe, ambient);
    }

    /// <summary>
    /// Returns the mix with the most oxygen and the least helium that stays within an oxygen limit and a narcotic limit at a depth.
    /// </summary>
    /// <param name="depth">The deepest depth the mix is breathed at, in meters or feet.</param>
    /// <param name="maxPo2">The oxygen partial pressure limit, in bar.</param>
    /// <param name="maximumEquivalentNarcoticDepth">The narcotic limit, as an equivalent narcotic depth in meters or feet.</param>
    /// <param name="settings">The planner settings giving the environment and whether oxygen counts as narcotic.</param>
    /// <param name="units">The units of the depths.</param>
    /// <returns>The mix; its helium is zero when air-like narcosis is already within the limit.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="depth"/> or <paramref name="maximumEquivalentNarcoticDepth"/> is negative, or <paramref name="maxPo2"/> is not greater than 0.
    /// </exception>
    /// <remarks>Oxygen is rounded down and helium up, so the mix never exceeds either limit.</remarks>
    public static BestMix BestMix(double depth, double maxPo2, double maximumEquivalentNarcoticDepth, PlannerSettings settings, UnitSystem units)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfNegative(depth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPo2);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumEquivalentNarcoticDepth);

        var environment = PlanMapper.ToSettings(settings, units);
        var ambient = AmbientConditions.PressureAtDepth(environment, UnitConversion.ToDepth(depth, units)).InBar;
        var limit = AmbientConditions.PressureAtDepth(environment, UnitConversion.ToDepth(maximumEquivalentNarcoticDepth, units)).InBar;

        var oxygen = Math.Max(1, (int)Math.Floor(Math.Min(maxPo2 / ambient, 1) * 100 + 1e-9));
        var narcotic = limit * NarcoticFractionOfAir(settings.OxygenNarcotic) / ambient;
        var helium = settings.OxygenNarcotic ? 1 - narcotic : 1 - oxygen / 100.0 - narcotic;
        var heliumPercent = Math.Clamp((int)Math.Ceiling(helium * 100 - 1e-9), 0, 100 - oxygen);
        return new BestMix(oxygen, heliumPercent);
    }

    internal static double EquivalentNarcoticDepth(
        double fractionO2, double fractionN2, Pressure ambient, DivePlanSettings environment, bool oxygenNarcotic, UnitSystem units)
    {
        var narcotic = oxygenNarcotic ? fractionO2 + fractionN2 : fractionN2;
        var equivalent = Pressure.FromMillibar(ambient.InMillibar * narcotic / NarcoticFractionOfAir(oxygenNarcotic));
        return UnitConversion.FromDepth(AmbientConditions.DepthAtPressure(environment, equivalent), units);
    }

    internal static double Density(double fractionO2, double fractionN2, double fractionHe, Pressure ambient) =>
        (fractionO2 * OxygenDensity + fractionN2 * NitrogenDensity + fractionHe * HeliumDensity) * ambient.InAta;

    private static double NarcoticFractionOfAir(bool oxygenNarcotic) => oxygenNarcotic ? 1 : GasMixture.Air.FractionN2;
}
