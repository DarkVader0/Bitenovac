using Bitenovac.DecompressionAlgorithms.Core.Abstractions;
using Bitenovac.DecompressionAlgorithms.Core.Calculations;
using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Replays a planned dive on a fresh algorithm a minute at a time, recording what a dive computer
/// would show at each step.
/// </summary>
internal static class DiveTrace
{
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(1);

    public static DiveMoment[] Calculate(IDecompressionAlgorithm algorithm, DivePlanRequest request, DecoPlan plan,
        UnitSystem units)
    {
        var segments = plan.ExpandedSegments;
        var settings = request.Settings;
        var firstStops = FirstStopOfEachLeg(segments);

        // Exposure adds up from what earlier dives left, so this dive's own share separates it exactly.
        var alone = OxygenExposure.Calculate(segments, settings);
        var carried = new OxygenExposureResult(
            Math.Max(0, plan.CentralNervousSystemFraction - alone.CentralNervousSystemFraction),
            Math.Max(0, plan.OxygenToleranceUnits - alone.OxygenToleranceUnits));

        var state = algorithm.BeginDive(request);
        var completed = new List<DiveSegment>();
        var runtime = TimeSpan.Zero;
        var depthMeter = 0.0;

        var moments = new List<DiveMoment>
        {
            Moment(TimeSpan.Zero, Depth.Zero, Depth.Zero, segments[0], carried, TimeSpan.Zero, settings, units)
        };

        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            var count = Math.Max(1, (int)Math.Ceiling(segment.Duration / Step));
            var travels = segment.Kind is SegmentKind.Descent or SegmentKind.Ascent;

            for (var step = 1; step <= count; step++)
            {
                var stepDepth = travels
                    ? Depth.FromMeter(depthMeter + (segment.Depth.InMeter - depthMeter) * step / count)
                    : segment.Depth;
                var piece = new DiveSegment(stepDepth, segment.Duration / count, segment.Gas, segment.Kind, segment.Loop);

                state = algorithm.LoadSegment(state, piece);
                runtime += piece.Duration;

                // Exposure is taken over the whole segment so far rather than piece by piece, which
                // rounds each piece on its own, so at the segment's end it matches the plan's.
                var elapsed = new DiveSegment(stepDepth, segment.Duration * step / count, segment.Gas, segment.Kind, segment.Loop);
                var exposure = OxygenExposure.Calculate([.. completed, elapsed], settings, carried);

                var ascending = segment.Kind is SegmentKind.Ascent or SegmentKind.Stop or SegmentKind.GasSwitch
                    or SegmentKind.SafetyStop;
                var ceiling = ascending
                    ? Depth.FromMeter(Math.Min(algorithm.AscentCeiling(state, firstStops[index]).InMeter, stepDepth.InMeter))
                    : algorithm.CurrentCeiling(state);

                // On an ascent the rest of the plan is the ascent itself.
                var timeToSurface = ascending ? plan.TotalRuntime - runtime : algorithm.TimeToSurface(state, request);

                moments.Add(Moment(runtime, stepDepth, ceiling, piece, exposure, timeToSurface, settings, units));
            }

            completed.Add(segment);
            depthMeter = segment.Depth.InMeter;
        }

        return [.. moments];
    }

    /// <summary>
    /// Returns, for every segment, the deepest decompression stop of the leg it belongs to.
    /// </summary>
    /// <remarks>
    /// A leg runs from one level to the next, or from the last level to the surface. The model
    /// anchors its gradient-factor slope at that deepest stop, so each ascent is traced against it.
    /// </remarks>
    private static Depth[] FirstStopOfEachLeg(IReadOnlyList<DiveSegment> segments)
    {
        var firstStops = new Depth[segments.Count];
        var legStart = 0;

        for (var index = 0; index <= segments.Count; index++)
        {
            if (index < segments.Count && segments[index].Kind != SegmentKind.Bottom)
            {
                continue;
            }

            var firstStop = segments.Skip(legStart).Take(index - legStart)
                .Where(static segment => segment.Kind == SegmentKind.Stop)
                .Select(static segment => segment.Depth)
                .DefaultIfEmpty(Depth.Zero)
                .Max();
            for (var leg = legStart; leg < index; leg++)
            {
                firstStops[leg] = firstStop;
            }

            legStart = index + 1;
        }

        return firstStops;
    }

    private static DiveMoment Moment(TimeSpan runtime, Depth depth, Depth ceiling, DiveSegment breathing,
        OxygenExposureResult exposure, TimeSpan? timeToSurface, DivePlanSettings settings, UnitSystem units) =>
        new(runtime,
            UnitConversion.FromDepth(depth, units),
            UnitConversion.FromDepth(ceiling, units),
            breathing.Gas.ToString(),
            breathing.Loop.InspiredOxygenPressure(breathing.Gas, AmbientConditions.PressureAtDepth(settings, depth)).InBar,
            exposure.CentralNervousSystemFraction * 100,
            exposure.OxygenToleranceUnits,
            timeToSurface);
}
