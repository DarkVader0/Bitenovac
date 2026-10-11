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
            Segment(9, 3, GasMixture.Air, SegmentKind.Ascent),
            Segment(9, 1, GasMixture.Air, SegmentKind.Stop),
            Segment(9, 2, GasMixture.Air, SegmentKind.Stop),
            Segment(6, 2, GasMixture.Air, SegmentKind.Stop),
            Segment(6, 2, Nitrox50, SegmentKind.GasSwitch),
            Segment(6, 3, Nitrox50, SegmentKind.Stop),
            Segment(6, 4, GasMixture.Oxygen, SegmentKind.Stop),
            Segment(6, 1, GasMixture.Oxygen, SegmentKind.Stop, ClosedCircuit),
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
        30,
        [new AscentViolation(Depth.FromMeter(20), Depth.FromMeter(10), Depth.FromMeter(12))]);

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
        Assert.False(result.IsValid);
        Assert.Equal(TimeSpan.Zero, result.TotalRuntime);
        Assert.Equal(TimeSpan.Zero, result.StopTime);
        Assert.Equal(0, result.MaximumDepth);
        Assert.Equal(0, result.CnsPercent);
        Assert.Equal(0, result.OxygenToleranceUnits);
        Assert.False(result.ReserveSatisfied);
        Assert.Empty(result.Stops);
        Assert.Empty(result.Cylinders);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void From_ShouldSummarizeTheDive_WhenItWasPlanned()
    {
        // Arrange
        var plan = CreatePlan();

        // Act
        var result = DiveResult.From(0, plan, UnitSystem.Metric);

        // Assert
        Assert.Equal(0, result.Index);
        Assert.Null(result.FailureMessage);
        Assert.True(result.Succeeded);
        Assert.False(result.IsValid);
        Assert.Equal(TimeSpan.FromMinutes(41), result.TotalRuntime);
        Assert.Equal(TimeSpan.FromMinutes(1 + 2 + 2 + 2 + 3 + 4 + 1), result.StopTime);
        Assert.Equal(30, result.MaximumDepth, Precision);
        Assert.Equal(25, result.CnsPercent, Precision);
        Assert.Equal(30, result.OxygenToleranceUnits, Precision);
        Assert.False(result.ReserveSatisfied);
    }

    [Fact]
    public void From_ShouldMergeOnlyConsecutiveStopsAtOneDepthOnOneGasAndLoop_WhenTheAscentSplitsStops()
    {
        // Arrange
        var plan = CreatePlan();

        // Act
        var result = DiveResult.From(0, plan, UnitSystem.Metric);

        // Assert
        Assert.Equal(
        [
            new StopRow(9, TimeSpan.FromMinutes(3), "Air", "OC"),
            new StopRow(6, TimeSpan.FromMinutes(2), "Air", "OC"),
            new StopRow(6, TimeSpan.FromMinutes(3), "NX50", "OC"),
            new StopRow(6, TimeSpan.FromMinutes(4), "O2", "OC"),
            new StopRow(6, TimeSpan.FromMinutes(1), "O2", "CCR @ 1.3")
        ], result.Stops);
    }

    [Fact]
    public void From_ShouldPairEachCylindersUsageWithItsReserve_WhenTheDiveWasPlanned()
    {
        // Arrange
        var plan = CreatePlan();

        // Act
        var result = DiveResult.From(0, plan, UnitSystem.Metric);

        // Assert
        Assert.Equal(
        [
            new CylinderRow("Air", CylinderPurpose.BottomGas, 1800, 200, 50, false, 600, 600, true),
            new CylinderRow("NX50", CylinderPurpose.DecoGas, 300, 200, 157, false, 200, 100, false)
        ], result.Cylinders);
        Assert.Equal([new ViolationRow(20, 10, 12)], result.Violations);
    }

    [Fact]
    public void From_ShouldShowFeetCubicFeetAndPsi_WhenTheUnitsAreImperial()
    {
        // Arrange
        var plan = CreatePlan();

        // Act
        var result = DiveResult.From(0, plan, UnitSystem.Imperial);

        // Assert
        Assert.Equal(30 * FeetPerMeter, result.MaximumDepth, Precision);
        Assert.Equal(9 * FeetPerMeter, result.Stops[0].Depth, Precision);
        var back = result.Cylinders[0];
        Assert.Equal(1800 / LitersPerCubicFoot, back.GasUsed, Precision);
        Assert.Equal(200 / BarPerPsi, back.StartPressure, Precision);
        Assert.Equal(50 / BarPerPsi, back.EndPressure, Precision);
        Assert.Equal(600 / LitersPerCubicFoot, back.RequiredReserve, Precision);
        Assert.Equal(600 / LitersPerCubicFoot, back.ProjectedRemaining, Precision);
        var violation = Assert.Single(result.Violations);
        Assert.Equal(20 * FeetPerMeter, violation.FromDepth, Precision);
        Assert.Equal(10 * FeetPerMeter, violation.ToDepth, Precision);
        Assert.Equal(12 * FeetPerMeter, violation.Ceiling, Precision);
    }
}
