using System.Text.RegularExpressions;
using Bitenovac.DecompressionAlgorithms.Core.Equipment;
using Bitenovac.DecompressionAlgorithms.Core.Planning;

namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed partial class ContingencyTests
{
    private const int Precision = 6;

    private static readonly CylinderInput BottomGas = TestFactory.Cylinder(size: 24, startPressure: 230);
    private static readonly CylinderInput Nitrox50 = TestFactory.Cylinder(oxygenPercent: 50, size: 11, startPressure: 200, purpose: CylinderPurpose.DecoGas);
    private static readonly CylinderInput Oxygen = TestFactory.Cylinder(oxygenPercent: 100, size: 7, startPressure: 200, purpose: CylinderPurpose.DecoGas);

    private static DiveInput Deep(params CylinderInput[] cylinders) =>
        TestFactory.Dive(depth: 40, minutes: 25, cylinders: cylinders.Length == 0 ? [BottomGas] : cylinders);

    private static DiveResult Calculate(DivePlanDocument plan) => PlanCalculator.Calculate(plan).Dives[0];

    [Fact]
    public void Calculate_ShouldPlanTheDiveLongerAndDeeperFirst_WhenTheDiveCanBePlanned()
    {
        // Arrange
        var plan = TestFactory.Plan(Deep());

        // Act
        var result = Calculate(plan);

        // Assert
        var extended = Assert.Single(result.Contingencies);
        Assert.Equal(ContingencyKind.Extended, extended.Kind);
        Assert.Equal(3, extended.ExtraMinutes, Precision);
        Assert.Equal(3, extended.ExtraDepth, Precision);
        Assert.Equal(43, extended.Result.MaximumDepth, Precision);
        Assert.True(extended.Result.TotalRuntime > result.TotalRuntime);
    }

    [Fact]
    public void Calculate_ShouldPlanTheSameDive_WhenTheExtrasAreZero()
    {
        // Arrange
        var settings = PlannerSettings.Default(UnitSystem.Metric) with { ContingencyExtraMinutes = 0, ContingencyExtraDepth = 0 };
        var plan = TestFactory.Plan(Deep()) with { Settings = settings };

        // Act
        var result = Calculate(plan);

        // Assert
        Assert.Equal(result.TotalRuntime, Assert.Single(result.Contingencies).Result.TotalRuntime);
    }

    [Fact]
    public void Calculate_ShouldUseTheExtrasTheSettingsGive_WhenTheyAreChanged()
    {
        // Arrange
        var defaults = Calculate(TestFactory.Plan(Deep())).Contingencies[0].Result;
        var settings = PlannerSettings.Default(UnitSystem.Metric) with { ContingencyExtraMinutes = 10, ContingencyExtraDepth = 6 };
        var plan = TestFactory.Plan(Deep()) with { Settings = settings };

        // Act
        var extended = Calculate(plan).Contingencies[0];

        // Assert
        Assert.Equal(10, extended.ExtraMinutes, Precision);
        Assert.Equal(6, extended.ExtraDepth, Precision);
        Assert.Equal(46, extended.Result.MaximumDepth, Precision);
        Assert.True(extended.Result.TotalRuntime > defaults.TotalRuntime);
    }

    [Fact]
    public void Calculate_ShouldExtendOnlyTheDeepestLevel_WhenTheDiveHasSeveralLevels()
    {
        // Arrange
        var dive = Deep() with { Levels = [new LevelInput(40, 15), new LevelInput(25, 20)] };
        var plan = TestFactory.Plan(dive);

        // Act
        var extended = Calculate(plan).Contingencies[0].Result;

        // Assert
        Assert.Equal(43, extended.MaximumDepth, Precision);
        Assert.Equal(25, extended.Profile.Last(static row => row.Kind == SegmentKind.Bottom).EndDepth, Precision);
    }

    [Fact]
    public void Calculate_ShouldPlanOneDiveForEveryDecoGasLost_WhenTheDiveCarriesSeveral()
    {
        // Arrange
        var plan = TestFactory.Plan(Deep(BottomGas, Nitrox50, Oxygen));
        var baseline = Calculate(plan);

        // Act
        var contingencies = baseline.Contingencies;

        // Assert
        Assert.Equal([ContingencyKind.Extended, ContingencyKind.LostGas, ContingencyKind.LostGas], contingencies.Select(static contingency => contingency.Kind));
        Assert.Equal(
            [GasMixture.FromPercent(50, 0).ToString(), GasMixture.FromPercent(100, 0).ToString()],
            contingencies.Skip(1).Select(static contingency => contingency.LostGas));
        Assert.All(contingencies.Skip(1), contingency => Assert.True(contingency.Result.StopTime >= baseline.StopTime));
        Assert.Contains(contingencies.Skip(1), contingency => contingency.Result.StopTime > baseline.StopTime);
    }

    [Fact]
    public void Calculate_ShouldLeaveTheLostGasOutOfThePlan_WhenItIsLost()
    {
        // Arrange
        var plan = TestFactory.Plan(Deep(BottomGas, Nitrox50));

        // Act
        var lost = Calculate(plan).Contingencies.Single(static contingency => contingency.Kind == ContingencyKind.LostGas);

        // Assert
        Assert.DoesNotContain(lost.Result.Cylinders, cylinder => cylinder.Gas == lost.LostGas);
        Assert.DoesNotContain(lost.Result.Profile, row => row.Gas == lost.LostGas);
    }

    [Fact]
    public void Calculate_ShouldPlanOneDiveForTheGas_WhenTwoDecoCylindersHoldTheSameGas()
    {
        // Arrange
        var plan = TestFactory.Plan(Deep(BottomGas, Nitrox50, Nitrox50));

        // Act
        var contingencies = Calculate(plan).Contingencies;

        // Assert
        Assert.Equal(2, contingencies.Count);
        Assert.Equal(ContingencyKind.LostGas, contingencies[1].Kind);
    }

    [Fact]
    public void Calculate_ShouldReportThatNoGasIsLeft_WhenTheOnlyCylinderIsADecoGas()
    {
        // Arrange
        var plan = TestFactory.Plan(TestFactory.Dive(depth: 12, minutes: 10, cylinders: [Nitrox50]));

        // Act
        var result = Calculate(plan);

        // Assert
        var lost = result.Contingencies.Single(static contingency => contingency.Kind == ContingencyKind.LostGas);
        Assert.False(lost.Result.Succeeded);
        Assert.Equal("No gas is left to breathe.", lost.Result.FailureMessage);
    }

    [Fact]
    public void Calculate_ShouldReportTheFailureOnTheContingency_WhenTheDeeperDiveCannotBePlanned()
    {
        // Arrange
        var plan = TestFactory.Plan(TestFactory.Dive(depth: 55, minutes: 5, cylinders: [TestFactory.Cylinder(size: 24, startPressure: 230)]));

        // Act
        var result = Calculate(plan);

        // Assert
        Assert.True(result.Succeeded);
        var extended = Assert.Single(result.Contingencies);
        Assert.False(extended.Result.Succeeded);
        Assert.NotEmpty(extended.Result.FailureMessage!);
    }

    [Fact]
    public void Calculate_ShouldCarryTheTimelineAndNoContingenciesOnTheContingencies_WhenTheDiveHasThem()
    {
        // Arrange
        var plan = TestFactory.Plan(Deep(BottomGas, Nitrox50));

        // Act
        var contingencies = Calculate(plan).Contingencies;

        // Assert
        Assert.All(contingencies.Where(static contingency => contingency.Result.Succeeded), contingency =>
        {
            Assert.NotEmpty(contingency.Result.Timeline);
            Assert.Empty(contingency.Result.Contingencies);
        });
    }

    [Fact]
    public void Calculate_ShouldPlanNoContingencies_WhenTheDiveCannotBePlanned()
    {
        // Arrange
        var plan = TestFactory.Plan(TestFactory.Dive(cylinders: [TestFactory.Cylinder(oxygenPercent: 100, purpose: CylinderPurpose.DecoGas)]));

        // Act
        var result = Calculate(plan);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(result.Contingencies);
    }

    [Fact]
    public void Calculate_ShouldUseTheDivesOwnExtras_WhenItHasItsOwnSettings()
    {
        // Arrange
        var own = PlannerSettings.Default(UnitSystem.Metric) with { ContingencyExtraMinutes = 5, ContingencyExtraDepth = 0 };
        var plan = TestFactory.Plan(Deep(), Deep() with { SurfaceIntervalMinutes = 120, Settings = own });

        // Act
        var dives = PlanCalculator.Calculate(plan).Dives;

        // Assert
        Assert.Equal(3, dives[0].Contingencies[0].ExtraMinutes, Precision);
        Assert.Equal(5, dives[1].Contingencies[0].ExtraMinutes, Precision);
        Assert.Equal(40, dives[1].Contingencies[0].Result.MaximumDepth, Precision);
    }

    [Theory]
    [InlineData(-1, 3, "Settings.ContingencyExtraMinutes")]
    [InlineData(3, -1, "Settings.ContingencyExtraDepth")]
    public void Calculate_ShouldReportTheField_WhenAnExtraIsNegative(double minutes, double depth, string path)
    {
        // Arrange
        var settings = PlannerSettings.Default(UnitSystem.Metric) with { ContingencyExtraMinutes = minutes, ContingencyExtraDepth = depth };
        var plan = TestFactory.Plan(Deep()) with { Settings = settings };

        // Act
        var calculation = PlanCalculator.Calculate(plan);

        // Assert
        Assert.Contains(calculation.Issues, issue => issue.Path == path);
    }

    [Fact]
    public void Convert_ShouldConvertTheContingencyDepthAndKeepTheTime_WhenTheUnitsChange()
    {
        // Arrange
        var plan = TestFactory.Plan(Deep());

        // Act
        var imperial = PlanConverter.Convert(plan, UnitSystem.Imperial);

        // Assert
        Assert.Equal(Math.Round(3 / 0.3048, 1, MidpointRounding.AwayFromZero), imperial.Settings.ContingencyExtraDepth, Precision);
        Assert.Equal(3, imperial.Settings.ContingencyExtraMinutes, Precision);
    }

    [Fact]
    public void Default_ShouldAddThreeMinutesAndThreeMetersOrTenFeet_WhenTheUnitSystemIsChosen()
    {
        // Arrange

        // Act
        var metric = PlannerSettings.Default(UnitSystem.Metric);
        var imperial = PlannerSettings.Default(UnitSystem.Imperial);

        // Assert
        Assert.Equal((3d, 3d), (metric.ContingencyExtraMinutes, metric.ContingencyExtraDepth));
        Assert.Equal((3d, 10d), (imperial.ContingencyExtraMinutes, imperial.ContingencyExtraDepth));
    }

    [Fact]
    public void Deserialize_ShouldUseTheDefaultExtras_WhenTheStoredPlanPredatesThem()
    {
        // Arrange
        var json = ExtrasPattern().Replace(DivePlanJson.Serialize(TestFactory.Plan(Deep())), "");

        // Act
        var restored = DivePlanJson.Deserialize(json);

        // Assert
        Assert.DoesNotContain("contingency", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal((3d, 3d), (restored.Settings.ContingencyExtraMinutes, restored.Settings.ContingencyExtraDepth));
    }

    [GeneratedRegex(@",?""contingencyExtra(Minutes|Depth)"":[0-9.eE+-]+")]
    private static partial Regex ExtrasPattern();
}
