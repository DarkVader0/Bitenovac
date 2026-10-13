using Bitenovac.DecompressionAlgorithms.Core.Equipment;
using Bitenovac.DecompressionAlgorithms.Core.Planning;

namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed class PlanCalculatorTests
{
    private const int Precision = 6;

    private static readonly DiveInput DecompressionDive = TestFactory.Dive(depth: 40, minutes: 25,
        cylinders: [TestFactory.Cylinder(size: 24, startPressure: 230)]);

    [Fact]
    public void Calculate_ShouldThrowArgumentNullException_WhenThePlanIsNull()
    {
        // Arrange
        DivePlanDocument plan = null!;

        // Act
        Action act = () => PlanCalculator.Calculate(plan);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Calculate_ShouldReturnTheIssuesAndNoDives_WhenThePlanIsInvalid()
    {
        // Arrange
        var plan = TestFactory.Plan(TestFactory.Dive(depth: 0));

        // Act
        var calculation = PlanCalculator.Calculate(plan);

        // Assert
        Assert.False(calculation.IsCalculated);
        Assert.Equal("Dives[0].Levels[0].Depth", Assert.Single(calculation.Issues).Path);
        Assert.Empty(calculation.Dives);
    }

    [Fact]
    public void Calculate_ShouldPlanTheDive_WhenTheNewPlanIsUnchanged()
    {
        // Arrange
        var plan = TestFactory.Plan();
        var descentMinutes = 30.0 / 18;

        // Act
        var calculation = PlanCalculator.Calculate(plan);

        // Assert
        Assert.True(calculation.IsCalculated);
        Assert.Empty(calculation.Issues);
        var dive = Assert.Single(calculation.Dives);
        Assert.True(dive.Succeeded, dive.FailureMessage);
        Assert.True(dive.IsValid);
        Assert.Equal(30, dive.MaximumDepth, 9);
        Assert.True(dive.TotalRuntime > TimeSpan.FromMinutes(20 + descentMinutes));
        Assert.True(dive.CnsPercent > 0);
        Assert.Single(dive.Cylinders);
    }

    [Fact]
    public void Calculate_ShouldPlanALaterDiveWithTheEarlierDivesTissueLoading_WhenDivesFollowEachOther()
    {
        // Arrange
        var alone = TestFactory.Plan(DecompressionDive);
        var repetitive = TestFactory.Plan(DecompressionDive, DecompressionDive with { SurfaceIntervalMinutes = 30 });

        // Act
        var single = PlanCalculator.Calculate(alone).Dives[0];
        var series = PlanCalculator.Calculate(repetitive).Dives;

        // Assert
        Assert.Equal(2, series.Count);
        Assert.Equal(single.StopTime, series[0].StopTime);
        Assert.Equal(1, series[1].Index);
        Assert.True(series[1].StopTime > series[0].StopTime);
    }
    
    [Fact]
    public void Calculate_ShouldCarryTheEarlierDivesOxygenExposure_WhenDivesFollowEachOther()
    {
        // Arrange
        var plan = TestFactory.Plan(DecompressionDive, DecompressionDive with { SurfaceIntervalMinutes = 30 });

        // Act
        var dives = PlanCalculator.Calculate(plan).Dives;

        // Assert
        Assert.True(dives[1].CnsPercent >= dives[0].CnsPercent * (1 + Math.Pow(0.5, 30.0 / 90)));
        Assert.True(dives[1].OxygenToleranceUnits >= dives[0].OxygenToleranceUnits * 2);
    }
    
    [Fact]
    public void Calculate_ShouldReportTheFailureOnTheDive_WhenNoCylinderCanBeBreathed()
    {
        // Arrange
        var plan = TestFactory.Plan(TestFactory.Dive(cylinders:
            [TestFactory.Cylinder(oxygenPercent: 100, size: 3, purpose: CylinderPurpose.DecoGas)]));

        // Act
        var calculation = PlanCalculator.Calculate(plan);

        // Assert
        Assert.True(calculation.IsCalculated);
        var dive = Assert.Single(calculation.Dives);
        Assert.False(dive.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(dive.FailureMessage));
    }

    [Fact]
    public void Calculate_ShouldPlanTheDiveAndReportTheMissingGas_WhenTheCylinderRunsOut()
    {
        // Arrange
        var plan = DivePlanDocument.CreateNew(TestFactory.PlanId, "Test plan", UnitSystem.Imperial, TestFactory.Now) with
        {
            Dives = [TestFactory.Dive(depth: 100, minutes: 60, cylinders:
                [TestFactory.Cylinder(size: 10, ratedPressure: 3000, startPressure: 3000)])]
        };

        // Act
        var calculation = PlanCalculator.Calculate(plan);

        // Assert
        var dive = Assert.Single(calculation.Dives);
        Assert.True(dive.Succeeded, dive.FailureMessage);
        Assert.NotEmpty(dive.Profile);
        var cylinder = Assert.Single(dive.Cylinders);
        Assert.True(cylinder.IsExhausted);
        Assert.Equal(0, cylinder.EndPressure, Precision);
        Assert.Equal(cylinder.GasUsed - 10, cylinder.Shortfall, Precision);
    }

    [Fact]
    public void Calculate_ShouldReportNoMissingGas_WhenTheCylinderHoldsEnough()
    {
        // Arrange
        var plan = TestFactory.Plan();

        // Act
        var calculation = PlanCalculator.Calculate(plan);

        // Assert
        var cylinder = Assert.Single(Assert.Single(calculation.Dives).Cylinders);
        Assert.False(cylinder.IsExhausted);
        Assert.Equal(0, cylinder.Shortfall);
    }

    [Fact]
    public void Calculate_ShouldPlanOnTheLoop_WhenTheDiveIsOnAClosedCircuitRebreather()
    {
        // Arrange
        var plan = TestFactory.Plan(TestFactory.Dive(depth: 45, minutes: 30,
            circuit: new ClosedCircuitInput(1.3, 1.3),
            cylinders:
            [
                TestFactory.Cylinder(oxygenPercent: 21, heliumPercent: 35, size: 3, purpose: CylinderPurpose.Diluent),
                TestFactory.Cylinder(oxygenPercent: 100, size: 3, purpose: CylinderPurpose.Oxygen)
            ]));

        // Act
        var dive = PlanCalculator.Calculate(plan).Dives[0];

        // Assert
        Assert.True(dive.Succeeded, dive.FailureMessage);
        Assert.Contains(dive.Profile, static phase => phase.Kind == SegmentKind.Stop && phase.Circuit == "CCR @ 1.3");
    }

    [Fact]
    public void Calculate_ShouldPlanOnTheLoop_WhenTheDiveIsOnASemiClosedRebreather()
    {
        // Arrange
        var plan = TestFactory.Plan(TestFactory.Dive(depth: 30, minutes: 30,
            circuit: new SemiClosedCircuitInput(0.1),
            cylinders: [TestFactory.Cylinder(oxygenPercent: 32, size: 11, purpose: CylinderPurpose.Diluent)]));

        // Act
        var dive = PlanCalculator.Calculate(plan).Dives[0];

        // Assert
        Assert.True(dive.Succeeded, dive.FailureMessage);
        Assert.All(dive.Profile, static phase => Assert.Equal("PSCR 1:10", phase.Circuit));
    }

    [Fact]
    public void Calculate_ShouldTraceTheCeilingFromLeavingTheSurfaceToSurfacing_WhenTheDiveIsPlanned()
    {
        // Arrange
        var plan = TestFactory.Plan(DecompressionDive);

        // Act
        var dive = Assert.Single(PlanCalculator.Calculate(plan).Dives);

        // Assert
        Assert.Equal(new CeilingPoint(TimeSpan.Zero, 0), dive.Ceiling[0]);
        Assert.Equal(dive.TotalRuntime, dive.Ceiling[^1].Runtime);
        Assert.All(dive.Ceiling.Zip(dive.Ceiling.Skip(1)),
            static pair => Assert.InRange(pair.Second.Runtime - pair.First.Runtime, TimeSpan.Zero, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void Calculate_ShouldKeepTheProfileAtOrBelowTheCeiling_WhenTheDiveHasStops()
    {
        // Arrange
        var plan = TestFactory.Plan(DecompressionDive);

        // Act
        var dive = Assert.Single(PlanCalculator.Calculate(plan).Dives);

        // Assert
        Assert.True(dive.IsValid);
        Assert.Contains(dive.Ceiling, static point => point.Depth > 0);
        Assert.All(dive.Ceiling, point => Assert.True(DepthAt(dive, point.Runtime) >= point.Depth - 1e-6,
            $"At {point.Runtime} the ceiling is {point.Depth} m, the diver at {DepthAt(dive, point.Runtime)} m."));
    }

    [Fact]
    public void Calculate_ShouldTraceNoCeiling_WhenTheDiveNeedsNoStops()
    {
        // Arrange
        var plan = TestFactory.Plan(TestFactory.Dive(depth: 6, minutes: 10));

        // Act
        var dive = Assert.Single(PlanCalculator.Calculate(plan).Dives);

        // Assert
        Assert.Equal(TimeSpan.Zero, dive.StopTime);
        Assert.All(dive.Ceiling, static point => Assert.Equal(0, point.Depth, Precision));
    }

    [Fact]
    public void Calculate_ShouldTraceTheCeilingBelowTheNextLevel_WhenAnAscentBetweenLevelsPassesIt()
    {
        // Arrange
        var plan = TestFactory.Plan(TestFactory.Dive(depth: 40, minutes: 30,
            cylinders: [TestFactory.Cylinder(size: 24, startPressure: 230)]) with
        {
            Levels = [new LevelInput(40, 30), new LevelInput(3, 5)]
        });

        // Act
        var dive = Assert.Single(PlanCalculator.Calculate(plan).Dives);

        // Assert
        var violation = Assert.Single(dive.Violations);
        var endOfLevel = dive.Profile.First(static phase => phase.Kind == SegmentKind.Bottom).Runtime;
        var ceiling = dive.Ceiling.Single(point => (point.Runtime - endOfLevel).Duration() < TimeSpan.FromSeconds(1)).Depth;
        Assert.Equal(violation.Ceiling, ceiling, Precision);
        Assert.True(ceiling > violation.ToDepth);
    }

    [Fact]
    public void Calculate_ShouldTraceNoCeiling_WhenTheDiveCannotBePlanned()
    {
        // Arrange
        var plan = TestFactory.Plan(TestFactory.Dive(cylinders:
            [TestFactory.Cylinder(oxygenPercent: 100, size: 3, purpose: CylinderPurpose.DecoGas)]));

        // Act
        var dive = Assert.Single(PlanCalculator.Calculate(plan).Dives);

        // Assert
        Assert.False(dive.Succeeded);
        Assert.Empty(dive.Ceiling);
    }

    [Fact]
    public void Calculate_ShouldEndTheTimelineOnTheDivesTotals_WhenTheDiveSurfaces()
    {
        // Arrange
        var plan = TestFactory.Plan(DecompressionDive);

        // Act
        var dive = Assert.Single(PlanCalculator.Calculate(plan).Dives);

        // Assert
        var first = dive.Timeline[0];
        var last = dive.Timeline[^1];
        Assert.Equal(TimeSpan.Zero, first.Runtime);
        Assert.Equal(0, first.CnsPercent, Precision);
        Assert.Equal(TimeSpan.Zero, first.TimeToSurface);
        Assert.Equal(dive.TotalRuntime, last.Runtime);
        Assert.Equal(0, last.Depth, Precision);
        Assert.Equal(dive.CnsPercent, last.CnsPercent, Precision);
        Assert.Equal(dive.OxygenToleranceUnits, last.OxygenToleranceUnits, Precision);
        Assert.Equal(TimeSpan.Zero, last.TimeToSurface);
    }

    [Fact]
    public void Calculate_ShouldGiveThePlannedAscentAsTheTimeToSurface_WhenTheLastLevelEnds()
    {
        // Arrange
        var plan = TestFactory.Plan(DecompressionDive);

        // Act
        var dive = Assert.Single(PlanCalculator.Calculate(plan).Dives);

        // Assert
        var endOfLevel = dive.Profile.Last(static phase => phase.Kind == SegmentKind.Bottom).Runtime;
        var moment = dive.Timeline.Single(moment => (moment.Runtime - endOfLevel).Duration() < TimeSpan.FromSeconds(1));
        Assert.NotNull(moment.TimeToSurface);
        Assert.Equal((dive.TotalRuntime - endOfLevel).TotalMinutes, moment.TimeToSurface.Value.TotalMinutes, Precision);
    }

    [Fact]
    public void Calculate_ShouldGiveTheRestOfTheAscentAsTheTimeToSurface_WhenTheDiveIsAscending()
    {
        // Arrange
        var plan = TestFactory.Plan(DecompressionDive);

        // Act
        var dive = Assert.Single(PlanCalculator.Calculate(plan).Dives);

        // Assert
        var endOfLevel = dive.Profile.Last(static phase => phase.Kind == SegmentKind.Bottom).Runtime;
        Assert.All(dive.Timeline.Where(moment => moment.Runtime > endOfLevel),
            moment => Assert.Equal(dive.TotalRuntime - moment.Runtime, moment.TimeToSurface));
    }

    [Fact]
    public void Calculate_ShouldLengthenTheTimeToSurface_WhenTheDiveStaysOnTheLevel()
    {
        // Arrange
        var plan = TestFactory.Plan(DecompressionDive);

        // Act
        var dive = Assert.Single(PlanCalculator.Calculate(plan).Dives);

        // Assert
        var level = dive.Profile.Single(static phase => phase.Kind == SegmentKind.Bottom);
        var onLevel = dive.Timeline
            .Where(moment => moment.Runtime > level.Runtime - level.Duration && moment.Runtime <= level.Runtime)
            .Select(static moment => moment.TimeToSurface!.Value)
            .ToArray();
        Assert.True(onLevel.Length > 10);
        Assert.True(onLevel[^1] > onLevel[0]);
    }

    [Fact]
    public void Calculate_ShouldGiveTheOxygenPressureOfTheGasAtItsDepth_WhenTheDiveIsOnTheLevel()
    {
        // Arrange
        var plan = TestFactory.Plan(DecompressionDive);

        // Act
        var dive = Assert.Single(PlanCalculator.Calculate(plan).Dives);

        // Assert
        var surface = dive.Timeline[0].OxygenPressure;
        var onLevel = dive.Timeline.First(static moment => Math.Abs(moment.Depth - 40) < 1e-9);
        const double saltWaterBarAt40Meters = 1030 * 9.80665 * 40 / 100_000;
        Assert.Equal("Air", onLevel.Gas);
        Assert.Equal(surface + 0.21 * saltWaterBarAt40Meters, onLevel.OxygenPressure, Precision);
    }

    [Fact]
    public void Calculate_ShouldStartTheTimelineWithTheExposureCarriedOver_WhenTheDiveFollowsAnother()
    {
        // Arrange
        var plan = TestFactory.Plan(DecompressionDive, TestFactory.Dive(depth: 18, minutes: 30, surfaceIntervalMinutes: 90));

        // Act
        var dives = PlanCalculator.Calculate(plan).Dives;

        // Assert
        Assert.Equal(dives[0].CnsPercent * 0.5, dives[1].Timeline[0].CnsPercent, Precision);
        Assert.Equal(dives[0].OxygenToleranceUnits, dives[1].Timeline[0].OxygenToleranceUnits, Precision);
        Assert.Equal(dives[1].CnsPercent, dives[1].Timeline[^1].CnsPercent, Precision);
    }

    [Fact]
    public void Calculate_ShouldMarkTheSafetyStopAndTraceNoCeiling_WhenTheDiveNeedsNoDecompression()
    {
        // Arrange
        var plan = TestFactory.Plan(TestFactory.Dive(depth: 12, minutes: 15)) with
        {
            Settings = PlannerSettings.Default(UnitSystem.Metric) with { SafetyStop = true }
        };

        // Act
        var dive = Assert.Single(PlanCalculator.Calculate(plan).Dives);

        // Assert
        var stop = Assert.Single(dive.Profile, static phase => phase.Kind is SegmentKind.Stop or SegmentKind.SafetyStop);
        Assert.Equal(SegmentKind.SafetyStop, stop.Kind);
        Assert.Equal(TimeSpan.FromMinutes(3), stop.Duration);
        Assert.Equal(TimeSpan.FromMinutes(3), dive.StopTime);
        Assert.All(dive.Timeline, static moment => Assert.Equal(0, moment.Ceiling, Precision));
    }

    [Fact]
    public void Calculate_ShouldFollowTheLastDecompressionStopWithTheSafetyStop_WhenTheDiveNeedsDecompression()
    {
        // Arrange
        var plan = TestFactory.Plan(DecompressionDive) with
        {
            Settings = PlannerSettings.Default(UnitSystem.Metric) with { SafetyStop = true }
        };

        // Act
        var dive = Assert.Single(PlanCalculator.Calculate(plan).Dives);

        // Assert
        var safetyIndex = dive.Profile.ToList().FindIndex(static phase => phase.Kind == SegmentKind.SafetyStop);
        var lastStop = dive.Profile[safetyIndex - 1];
        var safetyStop = dive.Profile[safetyIndex];
        Assert.Equal(SegmentKind.Stop, lastStop.Kind);
        Assert.Equal(lastStop.EndDepth, safetyStop.EndDepth, Precision);
        Assert.Equal(TimeSpan.FromMinutes(3), safetyStop.Duration);
        Assert.Single(dive.Profile, static phase => phase.Kind == SegmentKind.SafetyStop);
    }

    [Fact]
    public void Calculate_ShouldHaveNoTimeline_WhenTheDiveCannotBePlanned()
    {
        // Arrange
        var plan = TestFactory.Plan(TestFactory.Dive(cylinders:
            [TestFactory.Cylinder(oxygenPercent: 100, size: 3, purpose: CylinderPurpose.DecoGas)]));

        // Act
        var dive = Assert.Single(PlanCalculator.Calculate(plan).Dives);

        // Assert
        Assert.Empty(dive.Timeline);
    }

    private static double DepthAt(DiveResult dive, TimeSpan runtime)
    {
        var phase = dive.Profile.First(phase => phase.Runtime >= runtime);
        var start = phase.Runtime - phase.Duration;
        var fraction = phase.Duration > TimeSpan.Zero ? (runtime - start) / phase.Duration : 1.0;
        return phase.StartDepth + (phase.EndDepth - phase.StartDepth) * fraction;
    }
}
