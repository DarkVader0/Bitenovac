using Bitenovac.DecompressionAlgorithms.Core.Planning;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Represents the dive at one moment of its profile chart, in the plan's units.
/// </summary>
/// <param name="Runtime">The moment, from the start of the dive.</param>
/// <param name="Depth">The depth at that moment.</param>
/// <param name="Ceiling">The decompression ceiling at that moment, or <see langword="null"/> when there is none.</param>
/// <param name="Gas">The gas breathed at that moment.</param>
/// <param name="Kind">The phase of the dive at that moment.</param>
/// <param name="OxygenPressure">The inspired partial pressure of oxygen, in bar.</param>
/// <param name="CnsPercent">The central nervous system oxygen toxicity so far.</param>
/// <param name="OxygenToleranceUnits">The oxygen tolerance units so far.</param>
/// <param name="TimeToSurface">How long an ascent begun now would take, or <see langword="null"/> when unknown.</param>
public sealed record ChartReadout(
    TimeSpan Runtime,
    double Depth,
    double? Ceiling,
    string Gas,
    SegmentKind Kind,
    double OxygenPressure,
    double CnsPercent,
    double OxygenToleranceUnits,
    TimeSpan? TimeToSurface)
{
    /// <summary>
    /// Reads the dive at a moment, between the phase ends and the timeline samples the result holds.
    /// </summary>
    /// <param name="result">A dive that was planned.</param>
    /// <param name="minutes">The moment in minutes, kept within the dive.</param>
    /// <returns>The dive at that moment.</returns>
    /// <exception cref="ArgumentException"><paramref name="result"/> has no profile or no timeline.</exception>
    /// <remarks>
    /// Every phase ends on a timeline sample, so the two samples around a moment always share its
    /// gas and a straight line between them holds.
    /// </remarks>
    public static ChartReadout At(DiveResult result, double minutes)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Profile.Count == 0 || result.Timeline.Count == 0)
        {
            throw new ArgumentException("A dive that could not be planned has nothing to read.", nameof(result));
        }

        var runtime = TimeSpan.FromMinutes(Math.Clamp(minutes, 0, result.Profile[^1].Runtime.TotalMinutes));
        var phase = result.Profile.First(row => row.Runtime >= runtime);
        var start = phase.Runtime - phase.Duration;
        var depth = phase.Duration > TimeSpan.Zero
            ? phase.StartDepth + (phase.EndDepth - phase.StartDepth) * ((runtime - start) / phase.Duration)
            : phase.EndDepth;

        var (before, after, fraction) = Around(result.Timeline, runtime);
        var ceiling = Between(before.Ceiling, after.Ceiling, fraction);
        TimeSpan? timeToSurface = before.TimeToSurface is { } from && after.TimeToSurface is { } to
            ? from + (to - from) * fraction
            : null;

        return new ChartReadout(
            runtime,
            depth,
            ceiling > 0.05 ? ceiling : null,
            phase.Gas,
            phase.Kind,
            Between(before.OxygenPressure, after.OxygenPressure, fraction),
            Between(before.CnsPercent, after.CnsPercent, fraction),
            Between(before.OxygenToleranceUnits, after.OxygenToleranceUnits, fraction),
            timeToSurface);
    }

    private static (DiveMoment Before, DiveMoment After, double Fraction) Around(IReadOnlyList<DiveMoment> timeline, TimeSpan runtime)
    {
        for (var index = 1; index < timeline.Count; index++)
        {
            var (before, after) = (timeline[index - 1], timeline[index]);
            if (after.Runtime < runtime)
            {
                continue;
            }

            var span = after.Runtime - before.Runtime;
            return (before, after, span > TimeSpan.Zero ? (runtime - before.Runtime) / span : 1);
        }

        return (timeline[^1], timeline[^1], 0);
    }

    private static double Between(double from, double to, double fraction) => from + (to - from) * fraction;
}
