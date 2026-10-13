using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Export;

/// <summary>
/// Represents a point of a dive profile: a runtime and a depth, in the plan's units.
/// </summary>
/// <param name="Minutes">The time since leaving the surface, in minutes.</param>
/// <param name="Depth">The depth, in meters or feet.</param>
public readonly record struct ChartPoint(double Minutes, double Depth);

/// <summary>
/// Represents a decompression stop, a safety stop or a gas switch drawn on a profile chart.
/// </summary>
/// <param name="StartMinutes">The runtime the stop starts at, in minutes.</param>
/// <param name="EndMinutes">The runtime the stop ends at, in minutes.</param>
/// <param name="Depth">The depth of the stop, in meters or feet.</param>
/// <param name="Gas">The gas breathed, such as <c>NX50</c>.</param>
/// <param name="IsGasSwitch">Whether the stop is the switch onto <paramref name="Gas"/>.</param>
/// <param name="IsSafetyStop">Whether the stop is a safety stop, which the dive does not require.</param>
public sealed record ChartStop(double StartMinutes, double EndMinutes, double Depth, string Gas, bool IsGasSwitch,
    bool IsSafetyStop = false);

/// <summary>
/// Represents the rectangle a chart's data is drawn in, in the drawing's own coordinates.
/// </summary>
/// <param name="Left">The left edge.</param>
/// <param name="Top">The top edge, where the surface is drawn.</param>
/// <param name="Width">The width, greater than zero.</param>
/// <param name="Height">The height, greater than zero.</param>
public readonly record struct ChartArea(double Left, double Top, double Width, double Height);

/// <summary>
/// Represents the geometry of a depth-over-time chart of one dive, shared by every way it is drawn.
/// </summary>
/// <remarks>
/// Depth grows downwards, as on a dive computer: the surface is the top of the chart area.
/// </remarks>
public sealed class ProfileChart
{
    private static readonly double[] MinuteSteps = [1, 2, 5, 10, 15, 20, 30, 60, 120, 240];
    private static readonly double[] MeterSteps = [3, 5, 10, 15, 20, 30, 50, 100];
    private static readonly double[] FootSteps = [10, 20, 25, 50, 100, 200, 250, 500];

    private const int MaximumTicks = 6;

    private ProfileChart(IReadOnlyList<ChartPoint> points, IReadOnlyList<ChartStop> stops, IReadOnlyList<ChartPoint> ceiling,
        double maximumMinutes, IReadOnlyList<double> timeTicks, IReadOnlyList<double> depthTicks)
    {
        Points = points;
        Stops = stops;
        Ceiling = ceiling;
        MaximumMinutes = maximumMinutes;
        TimeTicks = timeTicks;
        DepthTicks = depthTicks;
    }

    /// <summary>
    /// Gets the profile from leaving the surface to surfacing, one point at the end of every phase.
    /// </summary>
    public IReadOnlyList<ChartPoint> Points { get; }

    /// <summary>
    /// Gets the stops and gas switches, in the order they are made.
    /// </summary>
    public IReadOnlyList<ChartStop> Stops { get; }

    /// <summary>
    /// Gets the decompression ceiling through the dive, at least once a minute; empty for a dive without one.
    /// </summary>
    /// <remarks>
    /// The profile stays at or below the ceiling; where it rises above, an ascent between levels
    /// passes it.
    /// </remarks>
    public IReadOnlyList<ChartPoint> Ceiling { get; }

    /// <summary>
    /// Gets the runtimes, in minutes, at which the time axis is labelled: from zero in equal steps, up to <see cref="MaximumMinutes"/>.
    /// </summary>
    public IReadOnlyList<double> TimeTicks { get; }

    /// <summary>
    /// Gets the depths at which the depth axis is labelled, from zero to <see cref="MaximumDepth"/>.
    /// </summary>
    public IReadOnlyList<double> DepthTicks { get; }

    /// <summary>
    /// Gets the runtime at the right edge of the chart, in minutes: the runtime rounded up to a whole minute.
    /// </summary>
    public double MaximumMinutes { get; }

    /// <summary>
    /// Gets the depth at the bottom edge of the chart: the maximum depth rounded up to a tick.
    /// </summary>
    public double MaximumDepth => DepthTicks[^1];

    /// <summary>
    /// Creates the chart of a calculated dive.
    /// </summary>
    /// <param name="result">The calculated dive.</param>
    /// <param name="units">The units of the plan the dive belongs to.</param>
    /// <returns>The chart geometry.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="result"/> has no profile, as for a dive that could not be planned.</exception>
    public static ProfileChart Create(DiveResult result, UnitSystem units)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Profile.Count == 0)
        {
            throw new ArgumentException("A dive that could not be planned has no profile to draw.", nameof(result));
        }

        var points = new List<ChartPoint> { new(0, 0) };
        var stops = new List<ChartStop>();
        var startMinutes = 0.0;

        foreach (var phase in result.Profile)
        {
            var endMinutes = phase.Runtime.TotalMinutes;
            points.Add(new ChartPoint(endMinutes, phase.EndDepth));

            if (phase.Kind is SegmentKind.Stop or SegmentKind.SafetyStop or SegmentKind.GasSwitch)
            {
                stops.Add(new ChartStop(startMinutes, endMinutes, phase.EndDepth, phase.Gas, phase.Kind == SegmentKind.GasSwitch,
                    phase.Kind == SegmentKind.SafetyStop));
            }

            startMinutes = endMinutes;
        }

        var deepest = points.Max(static point => point.Depth);
        var depthSteps = units == UnitSystem.Imperial ? FootSteps : MeterSteps;

        ChartPoint[] ceiling = result.Ceiling.Any(static point => point.Depth > 0)
            ? [.. result.Ceiling.Select(static point => new ChartPoint(point.Runtime.TotalMinutes, point.Depth))]
            : [];

        var maximumMinutes = Math.Max(1, Math.Ceiling(startMinutes - 1e-9));
        return new ProfileChart(points, stops, ceiling, maximumMinutes, TicksWithin(maximumMinutes, MinuteSteps), Ticks(deepest, depthSteps));
    }

    /// <summary>
    /// Returns the horizontal position of a runtime within the chart area.
    /// </summary>
    /// <param name="minutes">The runtime, in minutes.</param>
    /// <param name="area">The rectangle the data is drawn in.</param>
    /// <returns>The horizontal coordinate.</returns>
    public double X(double minutes, ChartArea area) => area.Left + minutes / MaximumMinutes * area.Width;

    /// <summary>
    /// Returns the vertical position of a depth within the chart area.
    /// </summary>
    /// <param name="depth">The depth, in meters or feet.</param>
    /// <param name="area">The rectangle the data is drawn in.</param>
    /// <returns>The vertical coordinate.</returns>
    public double Y(double depth, ChartArea area) => area.Top + depth / MaximumDepth * area.Height;

    /// <remarks>
    /// The step is the smallest one that labels the range with at most <see cref="MaximumTicks"/>
    /// intervals, and the axis always spans at least one step.
    /// </remarks>
    private static double[] Ticks(double maximum, double[] steps)
    {
        var step = Step(maximum, steps);
        var count = Math.Max(1, (int)Math.Ceiling(maximum / step - 1e-9));
        return [.. Enumerable.Range(0, count + 1).Select(index => index * step)];
    }

    /// <remarks>
    /// Unlike <see cref="Ticks"/>, the axis ends at <paramref name="maximum"/> itself, so the last
    /// label may fall short of the edge.
    /// </remarks>
    private static double[] TicksWithin(double maximum, double[] steps)
    {
        var step = Step(maximum, steps);
        var count = (int)Math.Floor(maximum / step + 1e-9);
        return [.. Enumerable.Range(0, count + 1).Select(index => index * step)];
    }

    private static double Step(double maximum, double[] steps) =>
        steps.FirstOrDefault(step => maximum / step <= MaximumTicks, steps[^1]);
}
