using System.Globalization;
using System.Text;
using Bitenovac.DecompressionAlgorithms.Core.Calculations;
using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionAlgorithms.Core.Planning;

/// <summary>
/// Represents the result of a decompression planning calculation.
/// </summary>
/// <remarks>
/// <para>
/// The plan holds the fully expanded dive profile including descent, working ascent, bottom, and
/// decompression stop segments, the gas consumed from each cylinder, the total runtime, the
/// per-cylinder reserve gas assessment, and the accrued oxygen toxicity exposure.
/// </para>
/// <para>
/// The expanded segments and gas usage are copied on construction.
/// </para>
/// </remarks>
public sealed class DecoPlan
{
    private readonly DiveSegment[] _expandedSegments;
    private readonly CylinderGasUsage[] _gasUsage;

    /// <summary>
    /// Initializes a new instance of the <see cref="DecoPlan"/> class.
    /// </summary>
    /// <param name="expandedSegments">The fully expanded sequence of dive segments.</param>
    /// <param name="gasUsage">The gas consumed from each cylinder over the dive.</param>
    /// <param name="totalRuntime">The total runtime of the dive, from leaving the surface to returning to it.</param>
    /// <param name="reserveGas">The per-cylinder reserve gas assessment for the dive.</param>
    /// <param name="centralNervousSystemFraction">
    /// The accrued central nervous system oxygen toxicity, as a fraction of the single-exposure
    /// limit.
    /// </param>
    /// <param name="oxygenToleranceUnits">The accrued pulmonary oxygen toxicity, in oxygen tolerance units.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="expandedSegments"/>, <paramref name="gasUsage"/>, or <paramref name="reserveGas"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="expandedSegments"/> is empty, or <paramref name="gasUsage"/> contains a
    /// <see langword="null"/> entry.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="totalRuntime"/> is negative, <paramref name="centralNervousSystemFraction"/> is negative, or
    /// <paramref name="oxygenToleranceUnits"/> is negative.
    /// </exception>
    public DecoPlan(
        IEnumerable<DiveSegment> expandedSegments,
        IEnumerable<CylinderGasUsage> gasUsage,
        TimeSpan totalRuntime,
        ReserveGasResult reserveGas,
        double centralNervousSystemFraction,
        double oxygenToleranceUnits)
    {
        ArgumentNullException.ThrowIfNull(expandedSegments);
        ArgumentNullException.ThrowIfNull(gasUsage);
        ArgumentNullException.ThrowIfNull(reserveGas);

        var segments = expandedSegments.ToArray();
        if (segments.Length == 0)
        {
            throw new ArgumentException("At least one segment must be supplied.", nameof(expandedSegments));
        }

        var usage = gasUsage.ToArray();
        if (Array.Exists(usage, static item => item is null))
        {
            throw new ArgumentException("The gas usage must not contain a null entry.", nameof(gasUsage));
        }

        if (totalRuntime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(totalRuntime), totalRuntime,
                "The total runtime must not be negative.");
        }

        if (centralNervousSystemFraction < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(centralNervousSystemFraction), centralNervousSystemFraction,
                "The central nervous system fraction must not be negative.");
        }

        if (oxygenToleranceUnits < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(oxygenToleranceUnits), oxygenToleranceUnits,
                "The oxygen tolerance units must not be negative.");
        }

        _expandedSegments = segments;
        _gasUsage = usage;
        TotalRuntime = totalRuntime;
        ReserveGas = reserveGas;
        CentralNervousSystemFraction = centralNervousSystemFraction;
        OxygenToleranceUnits = oxygenToleranceUnits;
    }

    /// <summary>
    /// Gets the fully expanded sequence of dive segments, in the order they occur.
    /// </summary>
    public IReadOnlyList<DiveSegment> ExpandedSegments => _expandedSegments;

    /// <summary>
    /// Gets the gas consumed from each cylinder over the dive.
    /// </summary>
    public IReadOnlyList<CylinderGasUsage> GasUsage => _gasUsage;

    /// <summary>
    /// Gets the total runtime of the dive, from leaving the surface to returning to it.
    /// </summary>
    public TimeSpan TotalRuntime { get; }

    /// <summary>
    /// Gets the per-cylinder reserve gas assessment for the dive.
    /// </summary>
    public ReserveGasResult ReserveGas { get; }

    /// <summary>
    /// Gets the accrued central nervous system oxygen toxicity.
    /// </summary>
    /// <value>
    /// The exposure on surfacing, as a fraction of the single-exposure limit.
    /// </value>
    public double CentralNervousSystemFraction { get; }

    /// <summary>
    /// Gets the accrued pulmonary oxygen toxicity.
    /// </summary>
    /// <value>
    /// The exposure on surfacing, in oxygen tolerance units.
    /// </value>
    public double OxygenToleranceUnits { get; }

    /// <summary>
    /// Returns a human-readable summary of the plan.
    /// </summary>
    /// <returns>The multi-line summary of the plan.</returns>
    /// <remarks>
    /// <para>
    /// The summary contains a header with the total runtime, and a schedule table
    /// with one line per phase showing the kind, depth, duration, runtime, and breathing gas. The
    /// oxygen exposure, the per-cylinder gas usage, and the reserve assessment follow.
    /// </para>
    /// <para>
    /// Durations and runtimes are rounded to whole minutes for display. Consecutive ascent segments
    /// are merged into a single line ending at the depth where the ascent pauses, so intermediate
    /// stop depths that are passed without holding do not appear. The output is formatted with the
    /// invariant culture.
    /// </para>
    /// </remarks>
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"Total runtime: {TotalRuntime.TotalMinutes}")
            .AppendLine()
            .AppendLine();

        static void AppendRow(StringBuilder builder,
            SegmentKind kind,
            Depth depth,
            TimeSpan duration,
            TimeSpan runtime,
            GasMixture gas,
            BreathingLoop loop) =>
            builder.Append(CultureInfo.InvariantCulture,
                    $"{kind,-9} {depth.InMeter,6:0.##} m  " +
                    $"{Math.Round(duration.TotalMinutes),4:0} min  " +
                    $"{Math.Round(runtime.TotalMinutes),4:0} min  {gas}" +
                    $"{(loop.Mode == DiveMode.OC ? string.Empty : $" ({loop})")}")
                .AppendLine();

        var runtime = TimeSpan.Zero;
        var pendingAscent = TimeSpan.Zero;
        var pendingAscentGas = GasMixture.Air;
        var pendingAscentLoop = BreathingLoop.OpenCircuit;
        var pendingAscentDepth = Depth.FromMeter(0);
        foreach (var segment in _expandedSegments)
        {
            runtime += segment.Duration;

            if (segment.Kind == SegmentKind.Ascent)
            {
                pendingAscent += segment.Duration;
                pendingAscentGas = segment.Gas;
                pendingAscentLoop = segment.Loop;
                pendingAscentDepth = segment.Depth;
                continue;
            }

            if (pendingAscent > TimeSpan.Zero)
            {
                AppendRow(builder, SegmentKind.Ascent, pendingAscentDepth, pendingAscent,
                    runtime - segment.Duration, pendingAscentGas, pendingAscentLoop);
                pendingAscent = TimeSpan.Zero;
            }

            AppendRow(builder, segment.Kind, segment.Depth, segment.Duration, runtime, segment.Gas, segment.Loop);
        }

        if (pendingAscent > TimeSpan.Zero)
        {
            AppendRow(builder, SegmentKind.Ascent, pendingAscentDepth, pendingAscent, runtime, pendingAscentGas,
                pendingAscentLoop);
        }

        builder.AppendLine()
            .Append(CultureInfo.InvariantCulture, $"CNS: {CentralNervousSystemFraction * 100:0.##} %")
            .AppendLine()
            .Append(CultureInfo.InvariantCulture, $"OTU: {OxygenToleranceUnits:0.##}")
            .AppendLine()
            .AppendLine();

        foreach (var usage in _gasUsage)
        {
            builder.Append(CultureInfo.InvariantCulture,
                    $"Cylinder {usage.Cylinder.Gas}: used {usage.GasUsed.InLiter:0.##} L, " +
                    $"end pressure {usage.EndPressure.InBar:0.##} bar")
                .AppendLine();
        }

        builder.Append(CultureInfo.InvariantCulture, $"Reserve satisfied: {ReserveGas.AllSatisfied}");
        return builder.ToString();
    }
}