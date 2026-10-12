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
}
