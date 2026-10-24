using Bitenovac.DecompressionAlgorithms.Core.Calculations;
using Bitenovac.DecompressionAlgorithms.Core.Equipment;
using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed class ChartReadoutTests
{
    private const int Precision = 6;

    private static readonly DiveResult Shallow = PlanCalculator.Calculate(TestFactory.Plan()).Dives[0];

    private static readonly DiveResult Decompression = PlanCalculator.Calculate(TestFactory.Plan(
        TestFactory.Dive(depth: 30, minutes: 50, cylinders: [TestFactory.Cylinder(), TestFactory.Cylinder(50, 0, 7, purpose: CylinderPurpose.DecoGas)])))
        .Dives[0];

    private static DiveSegment Segment(double depthMeters, double minutes, SegmentKind kind) =>
        new(Depth.FromMeter(depthMeters), TimeSpan.FromMinutes(minutes), GasMixture.Air, kind, default);

    private static DiveMoment Moment(double minutes, double cnsPercent, TimeSpan? timeToSurface) =>
        new(TimeSpan.FromMinutes(minutes), 0, 0, "Air", 0.21, cnsPercent, 0, timeToSurface);

    private static DiveResult Custom(DiveSegment[] segments, DiveMoment[] timeline)
    {
        var total = segments.Aggregate(TimeSpan.Zero, static (sum, segment) => sum + segment.Duration);
        var plan = new DecoPlan(segments, [], total, new ReserveGasResult([]), 0.25, 30);
        return DiveResult.From(0, plan, timeline, UnitSystem.Metric);
    }

    [Fact]
    public void At_ShouldThrowArgumentNullException_WhenTheResultIsNull()
    {
        // Arrange

        // Act
        Action act = () => ChartReadout.At(null!, 1);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void At_ShouldThrowArgumentException_WhenTheDiveWasNotPlanned()
    {
        // Arrange
        var failed = DiveResult.Failed(1, "No breathable gas at 40 m.");

        // Act
        Action act = () => ChartReadout.At(failed, 1);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void At_ShouldStartAtTheSurface_WhenTheMomentIsBeforeTheDive()
    {
        // Arrange
        var first = Shallow.Profile[0];

        // Act
        var readout = ChartReadout.At(Shallow, -5);

        // Assert
        Assert.Equal(0, readout.Runtime.TotalMinutes, Precision);
        Assert.Equal(first.StartDepth, readout.Depth, Precision);
    }

    [Fact]
    public void At_ShouldEndAtTheLastMoment_WhenTheMomentIsAfterTheDive()
    {
        // Arrange
        var last = Shallow.Profile[^1];

        // Act
        var readout = ChartReadout.At(Shallow, 10_000);

        // Assert
        Assert.Equal(last.Runtime.TotalMinutes, readout.Runtime.TotalMinutes, Precision);
        Assert.Equal(last.EndDepth, readout.Depth, Precision);
    }

    [Fact]
    public void At_ShouldInterpolateDepthAndTakeThePhase_WhenTheMomentIsInsideAMovingPhase()
    {
        // Arrange
        var phase = Shallow.Profile.First(row => row.Duration > TimeSpan.Zero && row.StartDepth != row.EndDepth);
        var moment = phase.Runtime - phase.Duration + phase.Duration / 2;

        // Act
        var readout = ChartReadout.At(Shallow, moment.TotalMinutes);

        // Assert
        Assert.Equal((phase.StartDepth + phase.EndDepth) / 2, readout.Depth, Precision);
        Assert.Equal(phase.Gas, readout.Gas);
        Assert.Equal(phase.Kind, readout.Kind);
    }

    [Fact]
    public void At_ShouldReadNoCeiling_WhenTheMomentIsAtTheSurface()
    {
        // Arrange

        // Act
        var readout = ChartReadout.At(Shallow, 0);

        // Assert
        Assert.Null(readout.Ceiling);
    }

    [Fact]
    public void At_ShouldReadTheSampleCeiling_WhenTheMomentIsAtADecompressionSample()
    {
        // Arrange
        var sample = Decompression.Timeline.First(moment => moment.Ceiling > 0.05);

        // Act
        var readout = ChartReadout.At(Decompression, sample.Runtime.TotalMinutes);

        // Assert
        Assert.NotNull(readout.Ceiling);
        Assert.Equal(sample.Ceiling, readout.Ceiling.Value, Precision);
        Assert.Equal(sample.TimeToSurface, readout.TimeToSurface);
    }

    [Fact]
    public void At_ShouldInterpolateCns_WhenTheMomentIsBetweenTwoSamples()
    {
        // Arrange
        var index = Enumerable.Range(1, Decompression.Timeline.Count - 1)
            .First(i => Decompression.Timeline[i].Runtime > Decompression.Timeline[i - 1].Runtime);
        var (before, after) = (Decompression.Timeline[index - 1], Decompression.Timeline[index]);
        var moment = before.Runtime + (after.Runtime - before.Runtime) / 2;

        // Act
        var readout = ChartReadout.At(Decompression, moment.TotalMinutes);

        // Assert
        Assert.Equal((before.CnsPercent + after.CnsPercent) / 2, readout.CnsPercent, Precision);
    }

    [Fact]
    public void At_ShouldReadTheEndDepth_WhenThePhaseTakesNoTime()
    {
        // Arrange
        var result = Custom(
            [Segment(20, 0, SegmentKind.Descent), Segment(20, 10, SegmentKind.Bottom)],
            [Moment(0, 0, null), Moment(10, 0, null)]);

        // Act
        var readout = ChartReadout.At(result, 0);

        // Assert
        Assert.Equal(20, readout.Depth, Precision);
    }

    [Fact]
    public void At_ShouldReadTheLaterSample_WhenTwoSamplesShareTheMoment()
    {
        // Arrange
        var result = Custom(
            [Segment(20, 10, SegmentKind.Bottom)],
            [Moment(5, 0.1, null), Moment(5, 0.3, null)]);

        // Act
        var readout = ChartReadout.At(result, 5);

        // Assert
        Assert.Equal(0.3, readout.CnsPercent, Precision);
    }

    [Fact]
    public void At_ShouldReadTheLastSample_WhenTheTimelineEndsBeforeTheDive()
    {
        // Arrange
        var result = Custom([Segment(20, 10, SegmentKind.Bottom)], [Moment(0, 0.2, null)]);

        // Act
        var readout = ChartReadout.At(result, 10);

        // Assert
        Assert.Equal(0.2, readout.CnsPercent, Precision);
    }

    [Fact]
    public void At_ShouldReadNoTimeToSurface_WhenASampleHasNone()
    {
        // Arrange
        var result = Custom(
            [Segment(20, 10, SegmentKind.Bottom)],
            [Moment(0, 0, null), Moment(10, 0, TimeSpan.FromMinutes(4))]);

        // Act
        var readout = ChartReadout.At(result, 5);

        // Assert
        Assert.Null(readout.TimeToSurface);
    }
}
