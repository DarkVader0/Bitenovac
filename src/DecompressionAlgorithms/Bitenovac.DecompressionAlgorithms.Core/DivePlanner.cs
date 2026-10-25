using Bitenovac.DecompressionAlgorithms.Core.Abstractions;
using Bitenovac.DecompressionAlgorithms.Core.Calculations;
using Bitenovac.DecompressionAlgorithms.Core.Equipment;
using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionAlgorithms.Core;

/// <summary>
/// Represents a dive planner that combines a decompression model with the shared, model-agnostic
/// planning logic.
/// </summary>
/// <remarks>
/// <para>
/// The planner walks the planned profile level by level. It builds the descent, working ascent,
/// and bottom segments, and advances the model's tissue state over each. An inter-level ascent
/// whose ceiling is deeper than its target is planned by the model, which inserts the decompression
/// stops it requires. It then asks the model for the final ascent to the surface and runs the
/// shared calculators for gas consumption, reserve gas, and oxygen toxicity over the complete
/// profile.
/// </para>
/// </remarks>
public sealed class DivePlanner
{
    private readonly IDecompressionAlgorithm _algorithm;

    /// <summary>
    /// Initializes a new instance of the <see cref="DivePlanner"/> class.
    /// </summary>
    /// <param name="algorithm">The decompression model used to track tissue loading and compute the ascent.</param>
    /// <exception cref="ArgumentNullException"><paramref name="algorithm"/> is <see langword="null"/>.</exception>
    public DivePlanner(IDecompressionAlgorithm algorithm)
    {
        ArgumentNullException.ThrowIfNull(algorithm);
        _algorithm = algorithm;
    }

    /// <summary>
    /// Creates the decompression plan for the specified request.
    /// </summary>
    /// <param name="request">The planning request supplying the profile, cylinders, and settings.</param>
    /// <returns>The decompression plan, including every decompression stop between levels and on the final ascent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The working phase is built and loaded into the model level by level. The model computes the
    /// final ascent to the surface, and the shared calculators run over the complete profile.
    /// </remarks>
    public DecoPlan CreatePlan(DivePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = request.Settings;
        var cylinders = request.Cylinders;

        var carried = CarriedOxygenExposure(request.PriorDives);
        var segments = BuildDive(request);

        var gasUsage = GasConsumption.Calculate(segments, cylinders, settings);
        var reserve = ReserveGas.Calculate(segments, cylinders, settings);
        var oxygen = OxygenExposure.Calculate(segments, settings, carried);
        var runtime = TotalRuntime(segments);

        return new DecoPlan(segments, gasUsage, runtime, reserve,
            oxygen.CentralNervousSystemFraction, oxygen.OxygenToleranceUnits);
    }
    
    /// <summary>
    /// Returns the oxygen exposure remaining from the prior dives when the planned dive begins.
    /// </summary>
    /// <param name="priorDives">The prior dives, in the order they were made.</param>
    /// <returns>The exposure carried into the planned dive.</returns>
    /// <remarks>
    /// Each prior dive is planned again, on top of the dives before it, because its exposure
    /// depends on the final ascent the model generates for it.
    /// </remarks>
    private OxygenExposureResult CarriedOxygenExposure(IReadOnlyList<PriorDive> priorDives)
    {
        var carried = OxygenExposureResult.None;

        for (var i = 0; i < priorDives.Count; i++)
        {
            var prior = priorDives[i];
            var request = new DivePlanRequest(prior.Profile, prior.Cylinders, prior.Settings, priorDives.Take(i));
            var segments = BuildDive(request);
            var onSurfacing = OxygenExposure.Calculate(segments, prior.Settings, carried);
            carried = OxygenExposure.AfterSurfaceInterval(onSurfacing, prior.SurfaceInterval);
        }

        return carried;
    }

    /// <summary>
    /// Builds the complete profile of a dive: the working phase walked level by level and the
    /// model's final ascent.
    /// </summary>
    /// <param name="request">The planning request supplying the profile, cylinders, and settings.</param>
    /// <returns>The expanded segments, in order.</returns>
    private List<DiveSegment> BuildDive(DivePlanRequest request)
    {
        var settings = request.Settings;
        var cylinders = request.Cylinders;

        var segments = new List<DiveSegment>();

        var state = _algorithm.BeginDive(request);
        var currentDepthMeter = 0.0;

        foreach (var target in request.Profile.Segments)
        {
            var targetDepthMeter = target.Depth.InMeter;

            if (targetDepthMeter > currentDepthMeter)
            {
                var descent = BuildTravel(currentDepthMeter, targetDepthMeter,
                    settings.DescentRateMetersPerMinute, SegmentKind.Descent, cylinders, settings, target.Loop);
                segments.Add(descent);
                state = _algorithm.LoadSegment(state, descent);
                currentDepthMeter = targetDepthMeter;
            }
            else if (targetDepthMeter < currentDepthMeter)
            {
                if (_algorithm.CurrentCeiling(state).InMeter > targetDepthMeter)
                {
                    segments.AddRange(_algorithm.CalculateAscent(state, request,
                        Depth.FromMeter(targetDepthMeter), target.Loop));
                }
                else
                {
                    var ascent = BuildTravel(currentDepthMeter, targetDepthMeter,
                        settings.AscentRateBelow75PercentMetersPerMinute, SegmentKind.Ascent, cylinders, settings,
                        target.Loop);
                    segments.Add(ascent);
                    state = _algorithm.LoadSegment(state, ascent);
                }

                currentDepthMeter = targetDepthMeter;
            }

            if (target.Duration <= TimeSpan.Zero)
            {
                continue;
            }

            var bottomCylinder = SelectBottomGas(cylinders, targetDepthMeter, settings, target.Loop);
            var bottom = new DiveSegment(Depth.FromMeter(targetDepthMeter), target.Duration,
                bottomCylinder.Gas, SegmentKind.Bottom, target.Loop);
            segments.Add(bottom);
            state = _algorithm.LoadSegment(state, bottom);
            currentDepthMeter = targetDepthMeter;
        }

        var finalAscent = _algorithm.CalculateFinalAscent(state, request);
        segments.AddRange(finalAscent);
        return segments;
    }

    /// <summary>
    /// Builds a travel segment between two depths at a given rate, breathing the gas selected for
    /// the depth at which the travel ends.
    /// </summary>
    /// <param name="fromDepthMeter">The depth, in meters, at which the travel begins.</param>
    /// <param name="toDepthMeter">The depth, in meters, at which the travel ends.</param>
    /// <param name="rateMetersPerMinute">The vertical rate of the travel, in meters per minute.</param>
    /// <param name="kind">The role of the travel segment within the dive.</param>
    /// <param name="cylinders">The cylinders available to the diver.</param>
    /// <param name="settings">The settings supplying the environment and the bottom oxygen partial pressure limit.</param>
    /// <param name="loop">The breathing apparatus through which the travel is breathed.</param>
    /// <returns>The travel segment.</returns>
    private static DiveSegment BuildTravel(double fromDepthMeter,
        double toDepthMeter,
        double rateMetersPerMinute,
        SegmentKind kind,
        IReadOnlyList<Cylinder> cylinders,
        DivePlanSettings settings,
        BreathingLoop loop)
    {
        var cylinder = SelectBottomGas(cylinders, toDepthMeter, settings, loop);
        var travelMeters = Math.Abs(toDepthMeter - fromDepthMeter);
        var duration = TimeSpan.FromMinutes(travelMeters / rateMetersPerMinute);
        return new DiveSegment(Depth.FromMeter(toDepthMeter), duration, cylinder.Gas, kind, loop);
    }

    /// <summary>
    /// Selects the richest supply gas for a working-phase segment at the given depth, within the
    /// bottom oxygen partial pressure limit.
    /// </summary>
    /// <param name="cylinders">The cylinders available to the diver.</param>
    /// <param name="depthMeter">The depth, in meters, at which the gas is breathed.</param>
    /// <param name="settings">The settings supplying the environment and the bottom oxygen partial pressure limit.</param>
    /// <param name="loop">The breathing apparatus through which the gas is supplied.</param>
    /// <returns>The cylinder holding the selected gas.</returns>
    /// <remarks>
    /// A rebreather draws only on its diluent supply. The limit is applied to the diluent breathed
    /// open circuit, since that is the exposure a flush or a bailout at that depth would produce.
    /// </remarks>
    private static Cylinder SelectBottomGas(IReadOnlyList<Cylinder> cylinders,
        double depthMeter,
        DivePlanSettings settings,
        BreathingLoop loop) =>
        GasSelector.SelectRichestGasAt(cylinders, depthMeter, settings.BottomPo2, settings, loop.SupplyPurpose);

    /// <summary>
    /// Returns the total runtime of the given segments.
    /// </summary>
    /// <param name="segments">The segments whose combined duration is required.</param>
    /// <returns>The sum of the durations of every segment.</returns>
    private static TimeSpan TotalRuntime(IEnumerable<DiveSegment> segments)
    {
        var total = TimeSpan.Zero;
        foreach (var segment in segments)
        {
            total += segment.Duration;
        }

        return total;
    }
}