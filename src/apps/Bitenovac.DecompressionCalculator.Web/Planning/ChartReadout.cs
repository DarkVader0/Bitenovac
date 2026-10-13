using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Represents the dive at one moment of its profile chart, in the plan's units.
/// </summary>
/// <param name="Runtime">The moment, from the start of the dive.</param>
/// <param name="Depth">The depth at that moment.</param>
/// <param name="Ceiling">The decompression ceiling at that moment, or <see langword="null"/> when there is none.</param>
/// <param name="Gas">The gas breathed at that moment.</param>
/// <param name="Kind">The phase of the dive at that moment.</param>
public sealed record ChartReadout(TimeSpan Runtime, double Depth, double? Ceiling, string Gas, SegmentKind Kind)
{
    /// <summary>
    /// Reads the dive at a moment, between the phase ends and ceiling samples the result holds.
    /// </summary>
    /// <param name="result">A dive that was planned.</param>
    /// <param name="minutes">The moment in minutes, kept within the dive.</param>
    /// <returns>The dive at that moment.</returns>
    /// <exception cref="ArgumentException"><paramref name="result"/> has no profile.</exception>
    public static ChartReadout At(DiveResult result, double minutes)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Profile.Count == 0)
        {
            throw new ArgumentException("A dive that could not be planned has no profile to read.", nameof(result));
        }

        var runtime = TimeSpan.FromMinutes(Math.Clamp(minutes, 0, result.Profile[^1].Runtime.TotalMinutes));
        var phase = result.Profile.First(row => row.Runtime >= runtime);
        var start = phase.Runtime - phase.Duration;
        var depth = phase.Duration > TimeSpan.Zero
            ? phase.StartDepth + (phase.EndDepth - phase.StartDepth) * ((runtime - start) / phase.Duration)
            : phase.EndDepth;

        var ceiling = CeilingAt(result.Ceiling, runtime);
        return new ChartReadout(runtime, depth, ceiling > 0.05 ? ceiling : null, phase.Gas, phase.Kind);
    }

    private static double CeilingAt(IReadOnlyList<CeilingPoint> points, TimeSpan runtime)
    {
        for (var index = 1; index < points.Count; index++)
        {
            var (before, after) = (points[index - 1], points[index]);
            if (after.Runtime < runtime)
            {
                continue;
            }

            var span = after.Runtime - before.Runtime;
            return span > TimeSpan.Zero
                ? before.Depth + (after.Depth - before.Depth) * ((runtime - before.Runtime) / span)
                : after.Depth;
        }

        return 0;
    }
}
