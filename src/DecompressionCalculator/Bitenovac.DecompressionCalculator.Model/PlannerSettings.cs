using Bitenovac.DecompressionAlgorithms.Core.Environment;
using Bitenovac.DecompressionAlgorithms.Core.Planning;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Represents the planner settings shared by every dive of a plan, in the plan's
/// <see cref="UnitSystem"/>.
/// </summary>
/// <param name="Altitude">The altitude of the dive site above sea level, in meters or feet.</param>
/// <param name="Salinity">The water the dives are made in.</param>
/// <param name="DescentRate">The descent rate, in meters or feet per minute.</param>
/// <param name="AscentRateDeep">The ascent rate below 75 % of the maximum depth, in meters or feet per minute.</param>
/// <param name="AscentRateMiddle">The ascent rate between 75 % and 50 % of the maximum depth, in meters or feet per minute.</param>
/// <param name="AscentRateToStops">The ascent rate from 50 % of the maximum depth to the stops, in meters or feet per minute.</param>
/// <param name="AscentRateShallow">The ascent rate over the last six meters, in meters or feet per minute.</param>
/// <param name="BottomSac">The surface air consumption on the bottom, in liters or cubic feet per minute.</param>
/// <param name="DecoSac">The surface air consumption during decompression, in liters or cubic feet per minute.</param>
/// <param name="BottomMetabolicOxygen">The metabolic oxygen consumption on the bottom, in liters per minute.</param>
/// <param name="DecoMetabolicOxygen">The metabolic oxygen consumption during decompression, in liters per minute.</param>
/// <param name="LoopVolume">The rebreather loop volume, in liters.</param>
/// <param name="BottomPo2">The highest oxygen partial pressure for bottom gases, in bar.</param>
/// <param name="DecoPo2">The highest oxygen partial pressure for decompression gases, in bar.</param>
/// <param name="MaximumOperatingDepthModel">How a gas's maximum operating depth is computed.</param>
/// <param name="ReserveStressFactor">The factor, at least one, by which breathing rises in an emergency ascent.</param>
/// <param name="ReserveTeamSize">The number of divers, at least one, the reserve gas must bring up.</param>
/// <param name="StopTimeIncrementMinutes">The resolution stops are rounded up to, in minutes.</param>
/// <param name="GasSwitchMinutes">The least time a gas switch takes, in minutes.</param>
/// <param name="OxygenBreakIntervalMinutes">How long oxygen is breathed before a break, in minutes.</param>
/// <param name="OxygenBreakDurationMinutes">How long each oxygen break lasts, in minutes.</param>
/// <param name="SafetyStop">Whether a three-minute safety stop is added: at 6 m on a dive without decompression stops, or at the end of the last decompression stop.</param>
/// <param name="LastStopAtSixMeters">Whether the last stop is at six meters (20 feet) instead of three (10 feet).</param>
/// <param name="SwitchAtRequiredStop">Whether gas switches wait for a required stop instead of happening at a gas's maximum operating depth.</param>
/// <param name="OxygenBreaks">Whether oxygen breaks are planned during long oxygen stops.</param>
/// <param name="ContingencyExtraMinutes">The time added to the deepest level in the contingency plan, in minutes.</param>
/// <param name="ContingencyExtraDepth">The depth added to the deepest level in the contingency plan, in meters or feet.</param>
/// <param name="OxygenNarcotic">
/// Whether oxygen counts as narcotic wherever an equivalent narcotic depth is given; on by default, the cautious choice.
/// </param>
public sealed record PlannerSettings(
    double Altitude,
    Salinity Salinity,
    double DescentRate,
    double AscentRateDeep,
    double AscentRateMiddle,
    double AscentRateToStops,
    double AscentRateShallow,
    double BottomSac,
    double DecoSac,
    double BottomMetabolicOxygen,
    double DecoMetabolicOxygen,
    double LoopVolume,
    double BottomPo2,
    double DecoPo2,
    MaximumOperatingDepthModel MaximumOperatingDepthModel,
    double ReserveStressFactor,
    int ReserveTeamSize,
    double StopTimeIncrementMinutes,
    double GasSwitchMinutes,
    double OxygenBreakIntervalMinutes,
    double OxygenBreakDurationMinutes,
    bool SafetyStop,
    bool LastStopAtSixMeters,
    bool SwitchAtRequiredStop,
    bool OxygenBreaks,
    double ContingencyExtraMinutes = 3,
    double ContingencyExtraDepth = 3,
    bool OxygenNarcotic = true)
{
    /// <summary>
    /// Returns the default settings for a unit system.
    /// </summary>
    /// <param name="units">The unit system the values are in.</param>
    /// <returns>Sea-level salt-water settings, with rates and gas consumption rounded to values usual in <paramref name="units"/>.</returns>
    public static PlannerSettings Default(UnitSystem units) => units switch
    {
        UnitSystem.Imperial => Metric with
        {
            DescentRate = 60,
            AscentRateDeep = 30,
            AscentRateMiddle = 30,
            AscentRateToStops = 30,
            AscentRateShallow = 3,
            BottomSac = 0.65,
            DecoSac = 0.5,
            ContingencyExtraDepth = 10
        },
        _ => Metric
    };

    private static PlannerSettings Metric { get; } = new(
        Altitude: 0,
        Salinity: Salinity.Salt,
        DescentRate: 18,
        AscentRateDeep: 9,
        AscentRateMiddle: 9,
        AscentRateToStops: 9,
        AscentRateShallow: 1,
        BottomSac: 18,
        DecoSac: 14,
        BottomMetabolicOxygen: 1,
        DecoMetabolicOxygen: 0.6,
        LoopVolume: 6,
        BottomPo2: 1.4,
        DecoPo2: 1.6,
        MaximumOperatingDepthModel: MaximumOperatingDepthModel.Realistic,
        ReserveStressFactor: 2,
        ReserveTeamSize: 2,
        StopTimeIncrementMinutes: 1,
        GasSwitchMinutes: 2,
        OxygenBreakIntervalMinutes: 20,
        OxygenBreakDurationMinutes: 5,
        SafetyStop: false,
        LastStopAtSixMeters: false,
        SwitchAtRequiredStop: false,
        OxygenBreaks: false,
        ContingencyExtraMinutes: 3,
        ContingencyExtraDepth: 3,
        OxygenNarcotic: true);
}
