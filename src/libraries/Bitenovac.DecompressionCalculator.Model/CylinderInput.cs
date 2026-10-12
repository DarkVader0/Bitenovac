using Bitenovac.DecompressionAlgorithms.Core.Equipment;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Represents one cylinder of a dive, as entered.
/// </summary>
/// <param name="OxygenPercent">The oxygen content, in percent.</param>
/// <param name="HeliumPercent">The helium content, in percent.</param>
/// <param name="Size">
/// In <see cref="UnitSystem.Metric"/>, the water volume in liters. In <see cref="UnitSystem.Imperial"/>,
/// the rated gas volume in cubic feet at <paramref name="RatedPressure"/>.
/// </param>
/// <param name="RatedPressure">The working pressure <paramref name="Size"/> is rated at, in psi. Used in <see cref="UnitSystem.Imperial"/> only.</param>
/// <param name="StartPressure">The fill pressure at the start of the dive, in bar or psi.</param>
/// <param name="Purpose">What the cylinder is carried for.</param>
public sealed record CylinderInput(
    double OxygenPercent,
    double HeliumPercent,
    double Size,
    double RatedPressure,
    double StartPressure,
    CylinderPurpose Purpose);
