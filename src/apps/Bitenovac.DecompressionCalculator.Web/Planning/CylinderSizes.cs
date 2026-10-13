using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Converts between the size the editor shows and the size a plan stores.
/// </summary>
/// <remarks>
/// A plan sizes an imperial cylinder as the free gas it holds at its rated pressure. The editor
/// asks for the free gas at the fill pressure instead, as divers name a tank ("80 cubic feet at
/// 3000 psi"), and stores it as a cylinder rated at its fill pressure.
/// </remarks>
public static class CylinderSizes
{
    private const int ImperialDecimals = 1;

    public static double Shown(CylinderInput cylinder, UnitSystem units) =>
        units == UnitSystem.Imperial && cylinder.RatedPressure > 0
            ? Math.Round(cylinder.Size * cylinder.StartPressure / cylinder.RatedPressure, ImperialDecimals)
            : cylinder.Size;

    public static CylinderInput WithSize(CylinderInput cylinder, double size, UnitSystem units) =>
        units == UnitSystem.Imperial
            ? cylinder with { Size = size, RatedPressure = cylinder.StartPressure }
            : cylinder with { Size = size };

    /// <remarks>An imperial cylinder keeps the free gas it shows, now at the new pressure.</remarks>
    public static CylinderInput WithPressure(CylinderInput cylinder, double pressure, UnitSystem units) =>
        units == UnitSystem.Imperial
            ? cylinder with { Size = Shown(cylinder, units), RatedPressure = pressure, StartPressure = pressure }
            : cylinder with { StartPressure = pressure };
}
