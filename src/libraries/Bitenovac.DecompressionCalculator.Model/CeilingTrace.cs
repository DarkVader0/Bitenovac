using Bitenovac.DecompressionAlgorithms.Core.Abstractions;
using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Provides a method for tracing the decompression ceiling through a planned dive.
/// </summary>
internal static class CeilingTrace
{
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Replays a planned dive through a fresh instance of its model and records the ceiling.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every segment is loaded in steps of at most a minute, a travel segment at depths spaced evenly
    /// along it, so the trace follows the tissues rather than only the phase ends.
    /// </para>
    /// <para>
    /// Until the final ascent the ceiling is the model's current one, the one an ascent between
    /// levels is checked against. The final ascent is everything after the last level; there the
    /// ceiling is the one the model applies on its way up from the first stop. That ascent is
    /// planned to clear each stop, so where the trace samples it between stops it is held at the
    /// diver's depth rather than drawn above it.
    /// </para>
    /// </remarks>
    public static CeilingPoint[] Calculate(IDecompressionAlgorithm algorithm, DivePlanRequest request,
        IReadOnlyList<DiveSegment> segments, UnitSystem units)
    {
        var lastLevel = -1;
        for (var index = 0; index < segments.Count; index++)
        {
            if (segments[index].Kind == SegmentKind.Bottom)
            {
                lastLevel = index;
            }
        }

        var firstStop = segments.Skip(lastLevel + 1)
            .Where(static segment => segment.Kind == SegmentKind.Stop)
            .Select(static segment => segment.Depth)
            .DefaultIfEmpty(Depth.Zero)
            .Max();

        var state = algorithm.BeginDive(request);
        var points = new List<CeilingPoint> { new(TimeSpan.Zero, 0) };
        var runtime = TimeSpan.Zero;
        var depthMeter = 0.0;

        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            var steps = Math.Max(1, (int)Math.Ceiling(segment.Duration / Step));
            var travels = segment.Kind is SegmentKind.Descent or SegmentKind.Ascent;

            for (var step = 1; step <= steps; step++)
            {
                var stepDepth = travels
                    ? Depth.FromMeter(depthMeter + (segment.Depth.InMeter - depthMeter) * step / steps)
                    : segment.Depth;
                var duration = segment.Duration / steps;

                state = algorithm.LoadSegment(state, new DiveSegment(stepDepth, duration, segment.Gas, segment.Kind, segment.Loop));
                runtime += duration;

                var ceiling = index > lastLevel
                    ? Depth.FromMeter(Math.Min(algorithm.AscentCeiling(state, firstStop).InMeter, stepDepth.InMeter))
                    : algorithm.CurrentCeiling(state);
                points.Add(new CeilingPoint(runtime, UnitConversion.FromDepth(ceiling, units)));
            }

            depthMeter = segment.Depth.InMeter;
        }

        return [.. points];
    }
}
