using Bitenovac.DecompressionAlgorithms.Core.Calculations;
using Bitenovac.DecompressionAlgorithms.Core.Equipment;
using Bitenovac.DecompressionAlgorithms.Core.Planning;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Represents one stop of a calculated dive, in the plan's units.
/// </summary>
/// <param name="Depth">The depth of the stop, in meters or feet.</param>
/// <param name="Duration">How long the stop lasts.</param>
/// <param name="Gas">The gas breathed, such as <c>Air</c>, <c>NX50</c>, or <c>TX18/45</c>.</param>
/// <param name="Circuit">The apparatus breathed from, such as <c>OC</c> or <c>CCR @ 1.3</c>.</param>
public sealed record StopRow(double Depth, TimeSpan Duration, string Gas, string Circuit);

/// <summary>
/// Represents what one cylinder delivered on a calculated dive, in the plan's units.
/// </summary>
/// <param name="Gas">The gas in the cylinder, such as <c>Air</c>, <c>NX50</c>, or <c>TX18/45</c>.</param>
/// <param name="Purpose">What the cylinder is carried for.</param>
/// <param name="GasUsed">The gas breathed from it, at the surface, in liters or cubic feet.</param>
/// <param name="StartPressure">The fill pressure, in bar or psi.</param>
/// <param name="EndPressure">The pressure left at the end of the dive, in bar or psi.</param>
/// <param name="IsExhausted">Whether the dive needs more gas than the cylinder holds.</param>
/// <param name="RequiredReserve">The gas that must remain for an emergency ascent, in liters or cubic feet.</param>
/// <param name="ProjectedRemaining">The gas projected to remain, in liters or cubic feet.</param>
/// <param name="ReserveSatisfied">Whether the remaining gas covers the required reserve.</param>
public sealed record CylinderRow(
    string Gas,
    CylinderPurpose Purpose,
    double GasUsed,
    double StartPressure,
    double EndPressure,
    bool IsExhausted,
    double RequiredReserve,
    double ProjectedRemaining,
    bool ReserveSatisfied);

/// <summary>
/// Represents a planned ascent that rises above the decompression ceiling, in the plan's units.
/// </summary>
/// <param name="FromDepth">The depth the ascent starts at, in meters or feet.</param>
/// <param name="ToDepth">The depth the ascent ends at, in meters or feet.</param>
/// <param name="Ceiling">The ceiling at the start of the ascent, in meters or feet.</param>
public sealed record ViolationRow(double FromDepth, double ToDepth, double Ceiling);

/// <summary>
/// Represents the outcome of one dive of a calculated plan, ready to show in the plan's units.
/// </summary>
/// <remarks>
/// A dive that could not be planned, for example because no cylinder is breathable at a depth,
/// has a <see cref="FailureMessage"/>, no rows, and zero for every figure.
/// </remarks>
public sealed class DiveResult
{
    private DiveResult(
        int index,
        string? failureMessage,
        bool isValid,
        TimeSpan totalRuntime,
        TimeSpan stopTime,
        double maximumDepth,
        double cnsPercent,
        double oxygenToleranceUnits,
        bool reserveSatisfied,
        StopRow[] stops,
        CylinderRow[] cylinders,
        ViolationRow[] violations)
    {
        Index = index;
        FailureMessage = failureMessage;
        IsValid = isValid;
        TotalRuntime = totalRuntime;
        StopTime = stopTime;
        MaximumDepth = maximumDepth;
        CnsPercent = cnsPercent;
        OxygenToleranceUnits = oxygenToleranceUnits;
        ReserveSatisfied = reserveSatisfied;
        Stops = stops;
        Cylinders = cylinders;
        Violations = violations;
    }

    /// <summary>
    /// Gets the zero-based position of the dive in the plan.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets why the dive could not be planned, or <see langword="null"/> when it was.
    /// </summary>
    public string? FailureMessage { get; }

    /// <summary>
    /// Gets a value indicating whether the dive was planned.
    /// </summary>
    public bool Succeeded => FailureMessage is null;

    /// <summary>
    /// Gets a value indicating whether every planned ascent stays below the decompression ceiling.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the time from leaving the surface to reaching it again.
    /// </summary>
    public TimeSpan TotalRuntime { get; }

    /// <summary>
    /// Gets the time spent on stops and gas switches, including a safety stop.
    /// </summary>
    public TimeSpan StopTime { get; }

    /// <summary>
    /// Gets the deepest point of the dive, in meters or feet.
    /// </summary>
    public double MaximumDepth { get; }

    /// <summary>
    /// Gets the central nervous system oxygen toxicity, in percent of the limit.
    /// </summary>
    public double CnsPercent { get; }

    /// <summary>
    /// Gets the whole-body oxygen exposure, in oxygen tolerance units.
    /// </summary>
    public double OxygenToleranceUnits { get; }

    /// <summary>
    /// Gets a value indicating whether every cylinder keeps its required reserve.
    /// </summary>
    public bool ReserveSatisfied { get; }

    /// <summary>
    /// Gets the stops, from the deepest to the last.
    /// </summary>
    public IReadOnlyList<StopRow> Stops { get; }

    /// <summary>
    /// Gets what each cylinder delivered, in the order the cylinders were entered.
    /// </summary>
    public IReadOnlyList<CylinderRow> Cylinders { get; }

    /// <summary>
    /// Gets the planned ascents that rise above the decompression ceiling.
    /// </summary>
    public IReadOnlyList<ViolationRow> Violations { get; }

    internal static DiveResult Failed(int index, string message) =>
        new(index, message, false, TimeSpan.Zero, TimeSpan.Zero, 0, 0, 0, false, [], [], []);

    internal static DiveResult From(int index, DecoPlan plan, UnitSystem units)
    {
        var segments = plan.ExpandedSegments;

        var stopTime = segments
            .Where(static segment => segment.Kind is SegmentKind.Stop or SegmentKind.GasSwitch)
            .Aggregate(TimeSpan.Zero, static (total, segment) => total + segment.Duration);

        var maximumDepth = segments.Max(static segment => segment.Depth);

        var cylinders = plan.GasUsage
            .Select((usage, cylinder) => ToCylinderRow(usage, plan.ReserveGas.CylinderStatuses[cylinder], units))
            .ToArray();

        var violations = plan.Violations
            .Select(violation => new ViolationRow(
                UnitConversion.FromDepth(violation.FromDepth, units),
                UnitConversion.FromDepth(violation.ToDepth, units),
                UnitConversion.FromDepth(violation.Ceiling, units)))
            .ToArray();

        return new DiveResult(
            index,
            failureMessage: null,
            plan.IsValid,
            plan.TotalRuntime,
            stopTime,
            UnitConversion.FromDepth(maximumDepth, units),
            plan.CentralNervousSystemFraction * 100,
            plan.OxygenToleranceUnits,
            plan.ReserveGas.AllSatisfied,
            GroupStops(segments, units),
            cylinders,
            violations);
    }

    /// <remarks>
    /// The final ascent can split one stop into several segments, around a gas switch or an oxygen
    /// break, so consecutive stop segments at the same depth on the same gas are shown as one stop.
    /// </remarks>
    private static StopRow[] GroupStops(IReadOnlyList<DiveSegment> segments, UnitSystem units)
    {
        var stops = new List<StopRow>();
        DiveSegment? previous = null;

        foreach (var segment in segments)
        {
            if (segment.Kind != SegmentKind.Stop)
            {
                previous = null;
                continue;
            }

            if (previous is { } last && last.Depth == segment.Depth && last.Gas == segment.Gas && last.Loop == segment.Loop)
            {
                stops[^1] = stops[^1] with { Duration = stops[^1].Duration + segment.Duration };
            }
            else
            {
                stops.Add(new StopRow(UnitConversion.FromDepth(segment.Depth, units), segment.Duration,
                    segment.Gas.ToString(), segment.Loop.ToString()));
            }

            previous = segment;
        }

        return [.. stops];
    }

    private static CylinderRow ToCylinderRow(CylinderGasUsage usage, CylinderReserveStatus reserve, UnitSystem units) => new(
        usage.Cylinder.Gas.ToString(),
        usage.Cylinder.Purpose,
        UnitConversion.FromGasVolume(usage.GasUsed, units),
        UnitConversion.FromPressure(usage.StartPressure, units),
        UnitConversion.FromPressure(usage.EndPressure, units),
        usage.IsExhausted,
        UnitConversion.FromGasVolume(reserve.RequiredReserve, units),
        UnitConversion.FromGasVolume(reserve.ProjectedRemaining, units),
        reserve.IsSatisfied);
}
