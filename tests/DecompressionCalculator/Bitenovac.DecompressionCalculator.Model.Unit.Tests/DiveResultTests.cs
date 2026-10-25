using Bitenovac.DecompressionAlgorithms.Core.Calculations;
using Bitenovac.DecompressionAlgorithms.Core.Equipment;
using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed class DiveResultTests
{
    private const int Precision = 9;
    private const double FeetPerMeter = 1 / 0.3048;
    private const double LitersPerCubicFoot = 28.316846592;
    private const double BarPerPsi = 0.06894757293168;

    private static readonly GasMixture Nitrox50 = GasMixture.FromPercent(50, 0);
    private static readonly BreathingLoop ClosedCircuit = BreathingLoop.ClosedCircuit(Pressure.FromBar(1.3));
    private static readonly Cylinder Back = new(GasMixture.Air, Volume.FromLiter(12), Pressure.FromBar(200), CylinderPurpose.BottomGas);
    private static readonly Cylinder Stage = new(Nitrox50, Volume.FromLiter(7), Pressure.FromBar(200), CylinderPurpose.DecoGas);

    private static DiveSegment Segment(double depthMeters, double minutes, GasMixture gas, SegmentKind kind, BreathingLoop loop = default) =>
        new(Depth.FromMeter(depthMeters), TimeSpan.FromMinutes(minutes), gas, kind, loop);

    private static DecoPlan CreatePlan() => new(
        [
            Segment(30, 2, GasMixture.Air, SegmentKind.Descent),
            Segment(30, 20, GasMixture.Air, SegmentKind.Bottom),
            Segment(30, 5, GasMixture.Air, SegmentKind.Bottom),
            Segment(9, 3, GasMixture.Air, SegmentKind.Ascent),
            Segment(9, 1, GasMixture.Air, SegmentKind.Stop),
            Segment(9, 2, GasMixture.Air, SegmentKind.Stop),
            Segment(6, 1, GasMixture.Air, SegmentKind.Ascent),
            Segment(6, 2, GasMixture.Air, SegmentKind.Stop),
            Segment(6, 2, Nitrox50, SegmentKind.GasSwitch),
            Segment(6, 3, Nitrox50, SegmentKind.Stop),
            Segment(6, 4, GasMixture.Oxygen, SegmentKind.Stop),
            Segment(6, 1, GasMixture.Oxygen, SegmentKind.Stop, ClosedCircuit),
            Segment(3, 1, GasMixture.Oxygen, SegmentKind.Ascent),
            Segment(0, 1, GasMixture.Oxygen, SegmentKind.Ascent)
        ],
        [
            new CylinderGasUsage(Back, Volume.FromLiter(1800), Pressure.FromBar(50)),
            new CylinderGasUsage(Stage, Volume.FromLiter(300), Pressure.FromBar(157))
        ],
        TimeSpan.FromMinutes(41),
        new ReserveGasResult(
        [
            new CylinderReserveStatus(Back, Volume.FromLiter(600), Volume.FromLiter(600)),
            new CylinderReserveStatus(Stage, Volume.FromLiter(200), Volume.FromLiter(100))
        ]),
        0.25,
        30);

    [Fact]
    public void Failed_ShouldCarryTheMessageAndNoFigures_WhenTheDiveCouldNotBePlanned()
    {
        // Arrange

        // Act
        var result = DiveResult.Failed(1, "No breathable gas at 40 m.");

        // Assert
        Assert.Equal(1, result.Index);
        Assert.Equal("No breathable gas at 40 m.", result.FailureMessage);
        Assert.False(result.Succeeded);
        Assert.Equal(TimeSpan.Zero, result.TotalRuntime);
        Assert.Equal(TimeSpan.Zero, result.StopTime);
        Assert.Equal(0, result.MaximumDepth);
        Assert.Equal(0, result.CnsPercent);
        Assert.Equal(0, result.OxygenToleranceUnits);
        Assert.False(result.ReserveSatisfied);
        Assert.Empty(result.Profile);
        Assert.Empty(result.Cylinders);
    }

    [Fact]
    public void From_ShouldSummarizeTheDive_WhenItWasPlanned()
    {
        // Arrange
        var plan = CreatePlan();

        // Act
        var result = DiveResult.From(0, plan, [], UnitSystem.Metric);

        // Assert
        Assert.Equal(0, result.Index);
        Assert.Null(result.FailureMessage);
        Assert.True(result.Succeeded);
        Assert.Equal(TimeSpan.FromMinutes(41), result.TotalRuntime);
        Assert.Equal(TimeSpan.FromMinutes(1 + 2 + 2 + 2 + 3 + 4 + 1), result.StopTime);
        Assert.Equal(30, result.MaximumDepth, Precision);
        Assert.Equal(25, result.CnsPercent, Precision);
        Assert.Equal(30, result.OxygenToleranceUnits, Precision);
        Assert.False(result.ReserveSatisfied);
    }

    [Fact]
    public void From_ShouldListEveryPhaseWithItsRuntimeMergingOnlySplitAscentsAndStops_WhenTheDiveWasPlanned()
    {
        // Arrange
        var plan = CreatePlan();

        // Act
        var result = DiveResult.From(0, plan, [], UnitSystem.Metric);

        // Assert
        Assert.Equal(
        [
            new ProfileRow(SegmentKind.Descent, 0, 30, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2), "Air", "OC"),
            new ProfileRow(SegmentKind.Bottom, 30, 30, TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(22), "Air", "OC"),
            new ProfileRow(SegmentKind.Bottom, 30, 30, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(27), "Air", "OC"),
            new ProfileRow(SegmentKind.Ascent, 30, 9, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(30), "Air", "OC"),
            new ProfileRow(SegmentKind.Stop, 9, 9, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(33), "Air", "OC"),
            new ProfileRow(SegmentKind.Ascent, 9, 6, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(34), "Air", "OC"),
            new ProfileRow(SegmentKind.Stop, 6, 6, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(36), "Air", "OC"),
            new ProfileRow(SegmentKind.GasSwitch, 6, 6, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(38), "NX50", "OC"),
            new ProfileRow(SegmentKind.Stop, 6, 6, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(41), "NX50", "OC"),
            new ProfileRow(SegmentKind.Stop, 6, 6, TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(45), "O2", "OC"),
            new ProfileRow(SegmentKind.Stop, 6, 6, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(46), "O2", "CCR @ 1.3"),
            new ProfileRow(SegmentKind.Ascent, 6, 0, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(48), "O2", "OC")
        ], result.Profile);
    }

    [Fact]
    public void From_ShouldPairEachCylindersUsageWithItsReserve_WhenTheDiveWasPlanned()
    {
        // Arrange
        var plan = CreatePlan();

        // Act
        var result = DiveResult.From(0, plan, [], UnitSystem.Metric);

        // Assert
        Assert.Equal(
        [
            ("Air", CylinderPurpose.BottomGas, 1800.0, 200.0, 50.0, false, 600.0, 600.0, true),
            ("NX50", CylinderPurpose.DecoGas, 300.0, 200.0, 157.0, false, 200.0, 100.0, false)
        ],
        result.Cylinders.Select(static c => (c.Gas, c.Purpose, c.GasUsed, c.StartPressure, c.EndPressure, c.IsExhausted, c.RequiredReserve, c.ProjectedRemaining, c.ReserveSatisfied)).ToArray());
    }

    [Fact]
    public void From_ShouldShowFeetCubicFeetAndPsi_WhenTheUnitsAreImperial()
    {
        // Arrange
        var plan = CreatePlan();

        // Act
        var result = DiveResult.From(0, plan, [], UnitSystem.Imperial);

        // Assert
        Assert.Equal(30 * FeetPerMeter, result.MaximumDepth, Precision);
        Assert.Equal(9 * FeetPerMeter, result.Profile[4].EndDepth, Precision);
        var back = result.Cylinders[0];
        Assert.Equal(1800 / LitersPerCubicFoot, back.GasUsed, Precision);
        Assert.Equal(200 / BarPerPsi, back.StartPressure, Precision);
        Assert.Equal(50 / BarPerPsi, back.EndPressure, Precision);
        Assert.Equal(600 / LitersPerCubicFoot, back.RequiredReserve, Precision);
        Assert.Equal(600 / LitersPerCubicFoot, back.ProjectedRemaining, Precision);
    }

    [Fact]
    public void TimeToSurface_ShouldBeTheAscentAfterTheLastLevel_WhenTheDiveWasPlanned()
    {
        // Arrange
        var result = DiveResult.From(0, CreatePlan(), [], UnitSystem.Metric);
        var lastLevelEnd = result.Profile.Last(static phase => phase.Kind is SegmentKind.Bottom or SegmentKind.Descent).Runtime;

        // Act
        var timeToSurface = result.TimeToSurface;

        // Assert
        Assert.Equal(result.TotalRuntime - lastLevelEnd, timeToSurface);
        Assert.True(timeToSurface > TimeSpan.Zero);
    }

    [Fact]
    public void TimeToSurface_ShouldBeZero_WhenTheDiveCouldNotBePlanned()
    {
        // Arrange
        var result = DiveResult.Failed(0, "No plan.");

        // Act
        var timeToSurface = result.TimeToSurface;

        // Assert
        Assert.Equal(TimeSpan.Zero, timeToSurface);
    }

    [Fact]
    public void WithStopsMerged_ShouldShowFewerPhasesAndTheSameFigures_WhenTheDiveHasAscentsIntoStops()
    {
        // Arrange
        var result = DiveResult.From(0, CreatePlan(), [], UnitSystem.Metric);

        // Act
        var merged = result.WithStopsMerged(true, true);

        // Assert
        Assert.True(merged.Profile.Count < result.Profile.Count);
        Assert.Equal(result.Profile[^1].Runtime, merged.Profile[^1].Runtime);
        Assert.Equal(result.TotalRuntime, merged.TotalRuntime);
        Assert.Equal(result.StopTime, merged.StopTime);
        Assert.Equal(result.Timeline.Count, merged.Timeline.Count);
        Assert.Equal(result.Cylinders.Count, merged.Cylinders.Count);
    }
}
