using Bitenovac.DecompressionAlgorithms.Units;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Converts between the size the editor shows, the cylinder's water volume, and the size a plan stores.
/// </summary>
/// <remarks>
/// A plan sizes an imperial cylinder as the free gas it holds at its rated pressure. The editor
/// asks for the water volume in cubic feet instead, so gas is size times pressure in both unit
/// systems; it stores that as a cylinder rated at one atmosphere, whose free gas at that pressure
/// is its water volume.
/// </remarks>
public static class CylinderSizes
{
    private const int ImperialDecimals = 3;

    private static readonly double AtmospherePsi = Pressure.FromAta(1).InPsi;

    public static double WaterVolume(CylinderInput cylinder, UnitSystem units) =>
        units == UnitSystem.Imperial && cylinder.RatedPressure > 0
            ? Math.Round(cylinder.Size * AtmospherePsi / cylinder.RatedPressure, ImperialDecimals)
            : cylinder.Size;

    public static CylinderInput WithWaterVolume(CylinderInput cylinder, double waterVolume, UnitSystem units) =>
        units == UnitSystem.Imperial
            ? cylinder with { Size = waterVolume, RatedPressure = AtmospherePsi }
            : cylinder with { Size = waterVolume };
}
