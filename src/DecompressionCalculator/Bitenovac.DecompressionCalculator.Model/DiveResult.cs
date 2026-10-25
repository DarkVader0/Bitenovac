using Bitenovac.DecompressionAlgorithms.Core.Calculations;
using Bitenovac.DecompressionAlgorithms.Core.Equipment;
using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Represents one phase of a calculated dive, in the plan's units.
/// </summary>
/// <param name="Kind">What the diver does: descend, stay at a level, ascend, stop, or switch gas.</param>
/// <param name="StartDepth">The depth the phase starts at, in meters or feet.</param>
/// <param name="EndDepth">The depth the phase ends at, in meters or feet.</param>
/// <param name="Duration">How long the phase lasts.</param>
/// <param name="Runtime">The time since leaving the surface when the phase ends.</param>
/// <param name="Gas">The gas breathed, such as <c>Air</c>, <c>NX50</c>, or <c>TX18/45</c>.</param>
/// <param name="Circuit">The apparatus breathed from, such as <c>OC</c> or <c>CCR @ 1.3</c>.</param>
public sealed record ProfileRow(
    SegmentKind Kind,
    double StartDepth,
    double EndDepth,
    TimeSpan Duration,
    TimeSpan Runtime,
    string Gas,
    string Circuit);

/// <summary>
/// Represents what one cylinder delivered on a calculated dive, in the plan's units.
/// </summary>
/// <param name="Gas">The gas in the cylinder, such as <c>Air</c>, <c>NX50</c>, or <c>TX18/45</c>.</param>
/// <param name="Purpose">What the cylinder is carried for.</param>
/// <param name="GasUsed">
/// The gas the dive needs from it, at the surface, in liters or cubic feet; more than it holds when
/// <see cref="Shortfall"/> is not zero.
/// </param>
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
    bool ReserveSatisfied)
{
    /// <summary>
    /// Gets the gas the dive needs beyond what the cylinder holds, at the surface, in liters or cubic
    /// feet; zero when the cylinder holds enough.
    /// </summary>
    public double Shortfall { get; init; }

    /// <summary>
    /// Gets the pressure at the start of the dive, in bar, whatever the units of the plan.
    /// </summary>
    public double StartBar { get; init; }

    /// <summary>
    /// Gets the pressure at the end of the dive, in bar, whatever the units of the plan.
    /// </summary>
    public double EndBar { get; init; }

    /// <summary>
    /// Gets the water volume of the cylinder, in liters, whatever the units of the plan.
    /// </summary>
    public double WaterLitres { get; init; }

    /// <summary>
    /// Gets the gas used, at the surface, in liters, whatever the units of the plan.
    /// </summary>
    public double UsedLitres { get; init; }

    /// <summary>
    /// Gets the gas required in reserve, at the surface, in liters, whatever the units of the plan.
    /// </summary>
    public double ReserveLitres { get; init; }

    /// <summary>
    /// Gets the gas left at the end of the dive, at the surface, in liters, whatever the units of the plan.
    /// </summary>
    public double LeftLitres { get; init; }

    /// <summary>
    /// Gets the gas the dive needs beyond what the cylinder holds, at the surface, in liters, whatever the units of the plan.
    /// </summary>
    public double MissingLitres { get; init; }
}

/// <summary>
/// Represents the decompression ceiling at one moment of a calculated dive, in the plan's units.
/// </summary>
/// <param name="Runtime">The time since leaving the surface.</param>
/// <param name="Depth">The shallowest depth the diver may ascend to, in meters or feet; zero when there is no ceiling.</param>
public sealed record CeilingPoint(TimeSpan Runtime, double Depth);

/// <summary>
/// Represents a calculated dive at one moment, as a dive computer would show it, in the plan's units.
/// </summary>
/// <param name="Runtime">The time since leaving the surface.</param>
/// <param name="Depth">The depth, in meters or feet.</param>
/// <param name="Ceiling">The decompression ceiling, in meters or feet; zero when there is none.</param>
/// <param name="Gas">The gas breathed, such as <c>Air</c> or <c>NX50</c>.</param>
/// <param name="OxygenPressure">The inspired partial pressure of oxygen, in bar.</param>
/// <param name="CnsPercent">The central nervous system oxygen toxicity so far, including earlier dives of the series.</param>
/// <param name="OxygenToleranceUnits">The oxygen tolerance units so far, including earlier dives of the series.</param>
/// <param name="TimeToSurface">
/// How long an ascent begun now would take, stops included; <see langword="null"/> when the model
/// cannot tell without disturbing the dive.
/// </param>
public sealed record DiveMoment(
    TimeSpan Runtime,
    double Depth,
    double Ceiling,
    string Gas,
    double OxygenPressure,
    double CnsPercent,
    double OxygenToleranceUnits,
    TimeSpan? TimeToSurface);

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
        TimeSpan totalRuntime,
        TimeSpan stopTime,
        double maximumDepth,
        double cnsPercent,
        double oxygenToleranceUnits,
        bool reserveSatisfied,
        ProfileRow[] profile,
        CylinderRow[] cylinders,
        DiveMoment[] timeline)
    {
        Index = index;
        FailureMessage = failureMessage;
        TotalRuntime = totalRuntime;
        StopTime = stopTime;
        MaximumDepth = maximumDepth;
        CnsPercent = cnsPercent;
        OxygenToleranceUnits = oxygenToleranceUnits;
        ReserveSatisfied = reserveSatisfied;
        Profile = profile;
        Cylinders = cylinders;
        Timeline = timeline;
        Ceiling = [.. timeline.Select(static moment => new CeilingPoint(moment.Runtime, moment.Ceiling))];
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
    /// Gets the time from leaving the surface to reaching it again.
    /// </summary>
    public TimeSpan TotalRuntime { get; }

    /// <summary>
    /// Gets the time from leaving the last level to reaching the surface: the ascent, its stops and gas switches.
    /// </summary>
    /// <remarks>
    /// A level of zero minutes leaves no phase of its own, so the ascent starts after the last level or descent.
    /// Zero for a dive that could not be planned.
    /// </remarks>
    public TimeSpan TimeToSurface => Profile.Count == 0
        ? TimeSpan.Zero
        : TotalRuntime - Profile.Last(static phase => phase.Kind is SegmentKind.Bottom or SegmentKind.Descent).Runtime;

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
    /// Gets the whole dive from leaving the surface to surfacing, phase by phase.
    /// </summary>
    public IReadOnlyList<ProfileRow> Profile { get; }

    /// <summary>
    /// Gets what each cylinder delivered, in the order the cylinders were entered.
    /// </summary>
    public IReadOnlyList<CylinderRow> Cylinders { get; }

    /// <summary>
    /// Gets the decompression ceiling through the dive, from leaving the surface to surfacing.
    /// </summary>
    /// <remarks>
    /// The ceiling is traced at least once a minute and at the end of every phase. It is the
    /// model's own continuous ceiling, not rounded to a stop depth; a planned dive stays at or below
    /// it.
    /// </remarks>
    public IReadOnlyList<CeilingPoint> Ceiling { get; }

    /// <summary>
    /// Gets the dive at every moment the ceiling is traced, from leaving the surface to surfacing.
    /// </summary>
    /// <remarks>
    /// The time to surface at a moment on the levels is the ascent the model would plan from there;
    /// on the final ascent it is the rest of the plan.
    /// </remarks>
    public IReadOnlyList<DiveMoment> Timeline { get; }

    /// <summary>
    /// Gets the dive planned again for what could go wrong, empty for a dive that could not be planned: first the
    /// deepest level longer and deeper, then one plan for each decompression gas lost.
    /// </summary>
    public IReadOnlyList<Contingency> Contingencies { get; internal set; } = [];

    /// <summary>
    /// Gets each gas as it is breathed on each level and at the deepest depth it is used, with its oxygen partial
    /// pressure, equivalent narcotic depth and density.
    /// </summary>
    public IReadOnlyList<GasCheckRow> GasChecks { get; internal set; } = [];

    /// <summary>
    /// Gets the gas switches that risk isobaric counterdiffusion.
    /// </summary>
    public IReadOnlyList<CounterdiffusionRow> Counterdiffusion { get; internal set; } = [];

    /// <summary>
    /// Returns the result with the chosen ascents or descents that end in a stop shown as part of that stop; see <see cref="ProfileMerge"/>.
    /// </summary>
    /// <param name="ascents">Whether an ascent that ends in a stop is folded into it.</param>
    /// <param name="descents">Whether a descent that ends in a stop is folded into it.</param>
    /// <returns>The result with the merged profile; every other figure is the same.</returns>
    public DiveResult WithStopsMerged(bool ascents, bool descents) =>
        new(Index, FailureMessage, TotalRuntime, StopTime, MaximumDepth, CnsPercent, OxygenToleranceUnits, ReserveSatisfied,
            [.. ProfileMerge.IntoStops(Profile, ascents, descents)], [.. Cylinders], [.. Timeline])
        {
            Contingencies = Contingencies,
            GasChecks = GasChecks,
            Counterdiffusion = Counterdiffusion
        };

    internal static DiveResult Failed(int index, string message) =>
        new(index, message, TimeSpan.Zero, TimeSpan.Zero, 0, 0, 0, false, [], [], []);

    internal static DiveResult From(int index, DecoPlan plan, IReadOnlyList<DiveMoment> timeline, UnitSystem units)
    {
        var segments = plan.ExpandedSegments;

        var stopTime = segments
            .Where(static segment => segment.Kind is SegmentKind.Stop or SegmentKind.SafetyStop or SegmentKind.GasSwitch)
            .Aggregate(TimeSpan.Zero, static (total, segment) => total + segment.Duration);

        var maximumDepth = segments.Max(static segment => segment.Depth);

        var cylinders = plan.GasUsage
            .Select((usage, cylinder) => ToCylinderRow(usage, plan.ReserveGas.CylinderStatuses[cylinder], units))
            .ToArray();

        return new DiveResult(
            index,
            failureMessage: null,
            plan.TotalRuntime,
            stopTime,
            UnitConversion.FromDepth(maximumDepth, units),
            plan.CentralNervousSystemFraction * 100,
            plan.OxygenToleranceUnits,
            plan.ReserveGas.AllSatisfied,
            BuildProfile(segments, units),
            cylinders,
            [.. timeline]);
    }

    /// <remarks>
    /// The final ascent is generated in short segments: the ascent rate changes at fixed fractions
    /// of the depth, and a gas switch or an oxygen break splits a stop. Consecutive ascent segments
    /// on one gas, and consecutive stop segments at one depth on one gas, are shown as one phase.
    /// </remarks>
    private static ProfileRow[] BuildProfile(IReadOnlyList<DiveSegment> segments, UnitSystem units)
    {
        var rows = new List<ProfileRow>();
        var startDepth = Depth.Zero;
        var runtime = TimeSpan.Zero;
        DiveSegment? previous = null;

        foreach (var segment in segments)
        {
            runtime += segment.Duration;

            if (previous is { } last && Continues(last, segment))
            {
                rows[^1] = rows[^1] with
                {
                    EndDepth = UnitConversion.FromDepth(segment.Depth, units),
                    Duration = rows[^1].Duration + segment.Duration,
                    Runtime = runtime
                };
            }
            else
            {
                rows.Add(new ProfileRow(segment.Kind, UnitConversion.FromDepth(startDepth, units),
                    UnitConversion.FromDepth(segment.Depth, units), segment.Duration, runtime,
                    segment.Gas.ToString(), segment.Loop.ToString()));
            }

            startDepth = segment.Depth;
            previous = segment;
        }

        return [.. rows];
    }

    private static bool Continues(DiveSegment previous, DiveSegment segment) =>
        previous.Kind == segment.Kind
        && previous.Gas == segment.Gas
        && previous.Loop == segment.Loop
        && segment.Kind switch
        {
            SegmentKind.Ascent => true,
            SegmentKind.Stop => previous.Depth == segment.Depth,
            _ => false
        };

    private static CylinderRow ToCylinderRow(CylinderGasUsage usage, CylinderReserveStatus reserve, UnitSystem units) => new(
        usage.Cylinder.Gas.ToString(),
        usage.Cylinder.Purpose,
        UnitConversion.FromGasVolume(usage.GasUsed, units),
        UnitConversion.FromPressure(usage.StartPressure, units),
        UnitConversion.FromPressure(usage.EndPressure, units),
        usage.IsExhausted,
        UnitConversion.FromGasVolume(reserve.RequiredReserve, units),
        UnitConversion.FromGasVolume(reserve.ProjectedRemaining, units),
        reserve.IsSatisfied)
    {
        Shortfall = UnitConversion.FromGasVolume(usage.Shortfall, units),
        StartBar = usage.StartPressure.InBar,
        EndBar = usage.EndPressure.InBar,
        WaterLitres = usage.Cylinder.Size.InLiter,
        UsedLitres = usage.GasUsed.InLiter,
        ReserveLitres = reserve.RequiredReserve.InLiter,
        LeftLitres = reserve.ProjectedRemaining.InLiter,
        MissingLitres = usage.Shortfall.InLiter
    };
}
