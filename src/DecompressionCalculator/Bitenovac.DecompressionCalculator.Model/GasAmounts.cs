using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Represents the gas of one cylinder in one unit family, so that every figure of it reads in the same units.
/// </summary>
/// <param name="Start">The pressure at the start of the dive, in bar or psi; or the gas it holds, in liters or cubic feet.</param>
/// <param name="End">The pressure at the end of the dive, or the gas it holds at its end.</param>
/// <param name="Used">The gas used, as the pressure it takes from the cylinder or as its volume at the surface.</param>
/// <param name="Reserve">The reserve the dive requires, in the same unit as <paramref name="Used"/>.</param>
/// <param name="Left">The gas left at the end of the dive, in the same unit as <paramref name="Used"/>.</param>
/// <param name="Missing">The gas the dive needs beyond the cylinder, in the same unit as <paramref name="Used"/>; zero when enough.</param>
public sealed record GasAmount(double Start, double End, double Used, double Reserve, double Left, double Missing);

/// <summary>
/// Provides a cylinder's gas in the unit family the diver chose: pressure in bar or psi, or volume in liters or cubic feet.
/// </summary>
public static class GasAmounts
{
    /// <summary>
    /// Returns the gas of a cylinder as the chosen quantity, in the units of the plan.
    /// </summary>
    /// <param name="cylinder">The cylinder's row in a result.</param>
    /// <param name="quantity">Whether the gas is shown as pressure or as volume.</param>
    /// <param name="units">The units of the plan.</param>
    /// <returns>The gas of the cylinder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cylinder"/> is <see langword="null"/>.</exception>
    public static GasAmount For(CylinderRow cylinder, GasQuantity quantity, UnitSystem units)
    {
        ArgumentNullException.ThrowIfNull(cylinder);

        return quantity == GasQuantity.Pressure
            ? new GasAmount(
                InPressureUnits(cylinder.StartBar, units),
                InPressureUnits(cylinder.EndBar, units),
                InPressureUnits(cylinder.UsedLitres / cylinder.WaterLitres, units),
                InPressureUnits(cylinder.ReserveLitres / cylinder.WaterLitres, units),
                InPressureUnits(cylinder.LeftLitres / cylinder.WaterLitres, units),
                InPressureUnits(cylinder.MissingLitres / cylinder.WaterLitres, units))
            : new GasAmount(
                InVolumeUnits(cylinder.StartBar * cylinder.WaterLitres, units),
                InVolumeUnits(cylinder.EndBar * cylinder.WaterLitres, units),
                InVolumeUnits(cylinder.UsedLitres, units),
                InVolumeUnits(cylinder.ReserveLitres, units),
                InVolumeUnits(cylinder.LeftLitres, units),
                InVolumeUnits(cylinder.MissingLitres, units));
    }

    private static double InPressureUnits(double bar, UnitSystem units) => UnitConversion.FromPressure(Pressure.FromBar(bar), units);

    private static double InVolumeUnits(double liters, UnitSystem units) => UnitConversion.FromGasVolume(Volume.FromLiter(liters), units);
}
