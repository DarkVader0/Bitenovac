using Bitenovac.DecompressionAlgorithms.Core.Environment;
using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Provides methods for converting between a plan's entered values and the libraries' units.
/// </summary>
internal static class UnitConversion
{
    public static Depth ToDepth(double value, UnitSystem units) =>
        units == UnitSystem.Imperial ? Depth.FromFeet(value) : Depth.FromMeter(value);

    public static double FromDepth(Depth depth, UnitSystem units) =>
        units == UnitSystem.Imperial ? depth.InFeet : depth.InMeter;

    public static Pressure ToPressure(double value, UnitSystem units) =>
        units == UnitSystem.Imperial ? Pressure.FromPsi(value) : Pressure.FromBar(value);

    public static double FromPressure(Pressure pressure, UnitSystem units) =>
        units == UnitSystem.Imperial ? pressure.InPsi : pressure.InBar;

    public static double ToLitersPerMinute(double value, UnitSystem units) =>
        units == UnitSystem.Imperial ? Volume.FromCubicFeet(value).InLiter : value;

    public static double FromGasVolume(Volume volume, UnitSystem units) =>
        units == UnitSystem.Imperial ? volume.InCubicFeet : volume.InLiter;

    /// <summary>
    /// Returns the water volume of a cylinder.
    /// </summary>
    public static Volume CylinderWaterVolume(CylinderInput cylinder, UnitSystem units) =>
        units == UnitSystem.Imperial
            ? Volume.FromCubicFeet(cylinder.Size) * (Pressure.FromAta(1) / Pressure.FromPsi(cylinder.RatedPressure))
            : Volume.FromLiter(cylinder.Size);

    public static Pressure SurfacePressure(double altitude, UnitSystem units) =>
        Pressure.FromMillibar(PhysicalConstants.AtmosphericPressureAtAltitudeMillibar(ToDepth(altitude, units).InMeter));
}
