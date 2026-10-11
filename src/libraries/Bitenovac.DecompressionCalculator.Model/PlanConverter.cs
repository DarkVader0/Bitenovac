using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Provides a method for converting a plan to another unit system.
/// </summary>
public static class PlanConverter
{
    private const int DepthDecimals = 1;
    private const int MetricGasRateDecimals = 1;
    private const int ImperialGasRateDecimals = 3;
    private const int PressureDecimals = 0;
    private const int CylinderSizeDecimals = 1;

    /// <summary>
    /// Returns the plan with every entered value converted to the specified unit system.
    /// </summary>
    /// <param name="plan">The plan to convert.</param>
    /// <param name="units">The unit system to convert to.</param>
    /// <returns>The converted plan, or <paramref name="plan"/> itself when it is already in <paramref name="units"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="plan"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="units"/> is not a defined unit system.</exception>
    /// <remarks>
    /// Converted values are rounded to the precision a diver enters them in, so converting back
    /// can differ from the original in the last digit. A metric cylinder becomes an imperial
    /// cylinder rated at its fill pressure.
    /// </remarks>
    public static DivePlanDocument Convert(DivePlanDocument plan, UnitSystem units)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!Enum.IsDefined(units))
        {
            throw new ArgumentOutOfRangeException(nameof(units), units, "The unit system is not defined.");
        }

        if (plan.Units == units)
        {
            return plan;
        }

        var from = plan.Units;
        return plan with
        {
            Units = units,
            Settings = ConvertSettings(plan.Settings, from, units),
            Dives = [.. plan.Dives.Select(dive => ConvertDive(dive, from, units))]
        };
    }

    private static PlannerSettings ConvertSettings(PlannerSettings settings, UnitSystem from, UnitSystem to) => settings with
    {
        Altitude = ConvertDepth(settings.Altitude, from, to),
        DescentRate = ConvertDepth(settings.DescentRate, from, to),
        AscentRateDeep = ConvertDepth(settings.AscentRateDeep, from, to),
        AscentRateMiddle = ConvertDepth(settings.AscentRateMiddle, from, to),
        AscentRateToStops = ConvertDepth(settings.AscentRateToStops, from, to),
        AscentRateShallow = ConvertDepth(settings.AscentRateShallow, from, to),
        BottomSac = ConvertGasRate(settings.BottomSac, from, to),
        DecoSac = ConvertGasRate(settings.DecoSac, from, to)
    };

    private static DiveInput ConvertDive(DiveInput dive, UnitSystem from, UnitSystem to) => dive with
    {
        Levels = [.. dive.Levels.Select(level => level with { Depth = ConvertDepth(level.Depth, from, to) })],
        Cylinders = [.. dive.Cylinders.Select(cylinder => ConvertCylinder(cylinder, from, to))]
    };

    private static CylinderInput ConvertCylinder(CylinderInput cylinder, UnitSystem from, UnitSystem to)
    {
        var waterVolume = UnitConversion.CylinderWaterVolume(cylinder, from);
        var startPressure = Round(UnitConversion.FromPressure(UnitConversion.ToPressure(cylinder.StartPressure, from), to), PressureDecimals);

        if (to == UnitSystem.Imperial)
        {
            var ratedGas = waterVolume * (Pressure.FromPsi(startPressure) / Pressure.FromAta(1));
            return cylinder with
            {
                Size = Round(ratedGas.InCubicFeet, CylinderSizeDecimals),
                RatedPressure = startPressure,
                StartPressure = startPressure
            };
        }

        return cylinder with
        {
            Size = Round(waterVolume.InLiter, CylinderSizeDecimals),
            RatedPressure = 0,
            StartPressure = startPressure
        };
    }

    private static double ConvertDepth(double value, UnitSystem from, UnitSystem to) =>
        Round(UnitConversion.FromDepth(UnitConversion.ToDepth(value, from), to), DepthDecimals);

    private static double ConvertGasRate(double value, UnitSystem from, UnitSystem to) =>
        Round(UnitConversion.FromGasVolume(Volume.FromLiter(UnitConversion.ToLitersPerMinute(value, from)), to),
            to == UnitSystem.Imperial ? ImperialGasRateDecimals : MetricGasRateDecimals);

    private static double Round(double value, int decimals) => Math.Round(value, decimals, MidpointRounding.AwayFromZero);
}
