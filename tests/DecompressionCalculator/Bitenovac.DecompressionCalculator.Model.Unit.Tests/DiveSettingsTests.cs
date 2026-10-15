using System.Text.Json;
using Bitenovac.DecompressionAlgorithms.Core.Equipment;

namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed class DiveSettingsTests
{
    private const int Precision = 6;
    private const double MetersPerFoot = 0.3048;
    private const double LitersPerCubicFoot = 28.316846592;

    private static readonly PlannerSettings Own = PlannerSettings.Default(UnitSystem.Metric) with { BottomSac = 30 };

    private static DiveInput Deep => TestFactory.Dive(depth: 40, minutes: 25, cylinders: [TestFactory.Cylinder(size: 24, startPressure: 230)]);

    [Fact]
    public void SettingsFor_ShouldReturnThePlansSettings_WhenTheDiveHasNone()
    {
        // Arrange
        var plan = TestFactory.Plan(TestFactory.Dive());

        // Act
        var settings = plan.SettingsFor(0);

        // Assert
        Assert.Equal(plan.Settings, settings);
    }

    [Fact]
    public void SettingsFor_ShouldReturnTheDivesOwnSettings_WhenTheDiveHasThem()
    {
        // Arrange
        var plan = TestFactory.Plan(TestFactory.Dive(), TestFactory.Dive(surfaceIntervalMinutes: 60) with { Settings = Own });

        // Act
        var first = plan.SettingsFor(0);
        var second = plan.SettingsFor(1);

        // Assert
        Assert.Equal(plan.Settings, first);
        Assert.Equal(Own, second);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void SettingsFor_ShouldThrowArgumentOutOfRangeException_WhenThereIsNoSuchDive(int dive)
    {
        // Arrange
        var plan = TestFactory.Plan(TestFactory.Dive());

        // Act
        Action act = () => plan.SettingsFor(dive);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void Equals_ShouldTellDivesApart_WhenOnlyTheirSettingsDiffer()
    {
        // Arrange
        var shared = TestFactory.Dive();
        var own = shared with { Settings = Own };

        // Act
        var equal = shared.Equals(own);

        // Assert
        Assert.False(equal);
        Assert.Equal(own, shared with { Settings = Own });
    }

    [Fact]
    public void Calculate_ShouldPlanTheDiveWithItsOwnSettings_WhenItHasThem()
    {
        // Arrange
        var followed = TestFactory.Plan(Deep);
        var overridden = TestFactory.Plan(Deep with { Settings = Own });

        // Act
        var shared = PlanCalculator.Calculate(followed).Dives[0];
        var heavier = PlanCalculator.Calculate(overridden).Dives[0];

        // Assert
        Assert.True(heavier.Cylinders.Sum(cylinder => cylinder.GasUsed) > shared.Cylinders.Sum(cylinder => cylinder.GasUsed));
    }

    [Fact]
    public void Calculate_ShouldLeaveTheOtherDivesOnThePlansSettings_WhenOneDiveHasItsOwn()
    {
        // Arrange
        var alone = TestFactory.Plan(Deep);
        var series = TestFactory.Plan(Deep, Deep with { SurfaceIntervalMinutes = 60, Settings = Own });

        // Act
        var single = PlanCalculator.Calculate(alone).Dives[0];
        var first = PlanCalculator.Calculate(series).Dives[0];

        // Assert
        Assert.Equal(single.Cylinders.Sum(cylinder => cylinder.GasUsed), first.Cylinders.Sum(cylinder => cylinder.GasUsed), Precision);
    }

    [Fact]
    public void Calculate_ShouldReportAnIssueOnTheDive_WhenItsOwnSettingsCannotBePlannedWith()
    {
        // Arrange
        var plan = TestFactory.Plan(TestFactory.Dive() with { Settings = Own with { BottomSac = 0 } });

        // Act
        var calculation = PlanCalculator.Calculate(plan);

        // Assert
        Assert.False(calculation.IsCalculated);
        Assert.Contains(calculation.Issues, issue => issue.Path == "Dives[0].Settings.BottomSac");
    }

    [Fact]
    public void Convert_ShouldConvertTheDivesOwnSettings_WhenTheUnitsChange()
    {
        // Arrange
        var plan = TestFactory.Plan(TestFactory.Dive() with { Settings = Own with { Altitude = 300 } });

        // Act
        var imperial = PlanConverter.Convert(plan, UnitSystem.Imperial);

        // Assert
        Assert.Equal(Math.Round(300 / MetersPerFoot, 1, MidpointRounding.AwayFromZero), imperial.Dives[0].Settings!.Altitude, Precision);
        Assert.Equal(Math.Round(30 / LitersPerCubicFoot, 3, MidpointRounding.AwayFromZero), imperial.Dives[0].Settings!.BottomSac, Precision);
    }

    [Fact]
    public void Convert_ShouldLeaveADiveWithoutSettingsWithout_WhenTheUnitsChange()
    {
        // Arrange
        var plan = TestFactory.Plan(TestFactory.Dive());

        // Act
        var imperial = PlanConverter.Convert(plan, UnitSystem.Imperial);

        // Assert
        Assert.Null(imperial.Dives[0].Settings);
    }

    [Fact]
    public void Deserialize_ShouldReturnAnEqualPlan_WhenADiveHasItsOwnSettings()
    {
        // Arrange
        var plan = TestFactory.Plan(TestFactory.Dive(), TestFactory.Dive(surfaceIntervalMinutes: 60) with { Settings = Own });

        // Act
        var restored = DivePlanJson.Deserialize(DivePlanJson.Serialize(plan));

        // Assert
        Assert.Equal(plan, restored);
        Assert.Equal(Own, restored.Dives[1].Settings);
    }

    [Fact]
    public void Deserialize_ShouldLeaveTheDivesOnThePlansSettings_WhenTheStoredPlanHasNoDiveSettings()
    {
        // Arrange
        var json = DivePlanJson.Serialize(TestFactory.Plan(TestFactory.Dive()));
        using var document = JsonDocument.Parse(json);

        // Act
        var restored = DivePlanJson.Deserialize(json);

        // Assert
        Assert.False(document.RootElement.GetProperty("dives")[0].TryGetProperty("settings", out var written) && written.ValueKind != JsonValueKind.Null);
        Assert.Null(restored.Dives[0].Settings);
    }
}
