using Bitenovac.DecompressionAlgorithms.Core.Equipment;
using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Provides methods for building the libraries' inputs from an entered plan.
/// </summary>
internal static class PlanMapper
{
    public static DivePlanSettings ToSettings(PlannerSettings settings, UnitSystem units) => new(
        UnitConversion.SurfacePressure(settings.Altitude, units),
        settings.Salinity,
        UnitConversion.ToDepth(settings.DescentRate, units).InMeter,
        UnitConversion.ToDepth(settings.AscentRateDeep, units).InMeter,
        UnitConversion.ToDepth(settings.AscentRateMiddle, units).InMeter,
        UnitConversion.ToDepth(settings.AscentRateToStops, units).InMeter,
        UnitConversion.ToDepth(settings.AscentRateShallow, units).InMeter,
        UnitConversion.ToLitersPerMinute(settings.BottomSac, units),
        UnitConversion.ToLitersPerMinute(settings.DecoSac, units),
        settings.BottomMetabolicOxygen,
        settings.DecoMetabolicOxygen,
        settings.LoopVolume,
        Pressure.FromBar(settings.BottomPo2),
        Pressure.FromBar(settings.DecoPo2),
        settings.MaximumOperatingDepthModel,
        Pressure.Zero,
        settings.ReserveStressFactor,
        settings.ReserveTeamSize,
        TimeSpan.FromMinutes(settings.StopTimeIncrementMinutes),
        TimeSpan.Zero,
        TimeSpan.FromMinutes(settings.GasSwitchMinutes),
        TimeSpan.FromMinutes(settings.OxygenBreakIntervalMinutes),
        TimeSpan.FromMinutes(settings.OxygenBreakDurationMinutes),
        settings.SafetyStop,
        settings.LastStopAtSixMeters,
        settings.SwitchAtRequiredStop,
        settings.OxygenBreaks,
        oxygenIsNarcotic: false);

    public static Cylinder ToCylinder(CylinderInput cylinder, UnitSystem units) => new(
        GasMixture.FromPercent(cylinder.OxygenPercent, cylinder.HeliumPercent),
        UnitConversion.CylinderWaterVolume(cylinder, units),
        UnitConversion.ToPressure(cylinder.StartPressure, units),
        cylinder.Purpose);

    /// <remarks>
    /// A semi-closed loop's oxygen drop is fixed for the whole dive at the bottom breathing rate,
    /// as the reference scenarios in <c>docs/reference-scenarios/semi-closed.md</c> model it.
    /// </remarks>
    public static BreathingLoop ToLoop(CircuitInput circuit, DivePlanSettings settings) => circuit switch
    {
        OpenCircuitInput => BreathingLoop.OpenCircuit,
        ClosedCircuitInput closed => BreathingLoop.ClosedCircuit(
            Pressure.FromBar(closed.BottomSetpoint),
            Pressure.FromBar(closed.DecoSetpoint)),
        SemiClosedCircuitInput semiClosed => BreathingLoop.SemiClosed(
            semiClosed.DumpRatio,
            BreathingLoop.SemiClosedOxygenDropCoefficient(
                semiClosed.DumpRatio,
                settings.BottomMetabolicOxygenConsumptionLitersPerMinute,
                settings.BottomSacLitersPerMinute,
                settings.SurfacePressure)),
        _ => throw new ArgumentOutOfRangeException(nameof(circuit), circuit.GetType().Name, "The breathing apparatus is not supported.")
    };

    /// <remarks>
    /// The planner chooses the gas breathed on each level from the cylinders, so the gas recorded
    /// on a level only names the first cylinder.
    /// </remarks>
    public static DiveProfile ToProfile(DiveInput dive, IReadOnlyList<Cylinder> cylinders, BreathingLoop loop, UnitSystem units) =>
        new(dive.Levels.Select(level => new DiveSegment(
            UnitConversion.ToDepth(level.Depth, units),
            TimeSpan.FromMinutes(level.DurationMinutes),
            cylinders[0].Gas,
            SegmentKind.Bottom,
            loop)));
}
