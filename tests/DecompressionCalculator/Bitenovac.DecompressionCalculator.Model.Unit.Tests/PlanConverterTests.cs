using Bitenovac.DecompressionAlgorithms.Core.Equipment;

namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed class PlanConverterTests
{
    private const int Precision = 9;
    private const double MetersPerFoot = 0.3048;
    private const double BarPerPsi = 0.06894757293168;
    private const double LitersPerCubicFoot = 28.316846592;
    private const double BarPerAtmosphere = 1.01325;

    private static double Round(double value, int decimals) => Math.Round(value, decimals, MidpointRounding.AwayFromZero);

    [Fact]
    public void Convert_ShouldThrowArgumentNullException_WhenThePlanIsNull()
    {
        // Arrange
        DivePlanDocument plan = null!;

        // Act
        Action act = () => PlanConverter.Convert(plan, UnitSystem.Imperial);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Convert_ShouldThrowArgumentOutOfRangeException_WhenTheUnitSystemIsUndefined()
    {
        // Arrange
        var plan = TestFactory.Plan();

        // Act
        Action act = () => PlanConverter.Convert(plan, (UnitSystem)99);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void Convert_ShouldReturnThePlanItself_WhenItIsAlreadyInTheUnitSystem()
    {
        // Arrange
        var plan = TestFactory.Plan();

        // Act
        var converted = PlanConverter.Convert(plan, UnitSystem.Metric);

        // Assert
        Assert.Same(plan, converted);
    }

    [Fact]
    public void Convert_ShouldRoundDepthsRatesAndBreathingRatesToFeetAndCubicFeet_WhenConvertingToImperial()
    {
        // Arrange
        var plan = TestFactory.Plan() with { Settings = PlannerSettings.Default(UnitSystem.Metric) with { Altitude = 400 } };

        // Act
        var converted = PlanConverter.Convert(plan, UnitSystem.Imperial);

        // Assert
        Assert.Equal(UnitSystem.Imperial, converted.Units);
        Assert.Equal(Round(400 / MetersPerFoot, 1), converted.Settings.Altitude, Precision);
        Assert.Equal(Round(18 / MetersPerFoot, 1), converted.Settings.DescentRate, Precision);
        Assert.Equal(Round(9 / MetersPerFoot, 1), converted.Settings.AscentRateDeep, Precision);
        Assert.Equal(Round(9 / MetersPerFoot, 1), converted.Settings.AscentRateMiddle, Precision);
        Assert.Equal(Round(9 / MetersPerFoot, 1), converted.Settings.AscentRateToStops, Precision);
        Assert.Equal(Round(1 / MetersPerFoot, 1), converted.Settings.AscentRateShallow, Precision);
        Assert.Equal(Round(18 / LitersPerCubicFoot, 3), converted.Settings.BottomSac, Precision);
        Assert.Equal(Round(14 / LitersPerCubicFoot, 3), converted.Settings.DecoSac, Precision);
        Assert.Equal(Round(30 / MetersPerFoot, 1), Assert.Single(converted.Dives[0].Levels).Depth, Precision);
    }

    [Fact]
    public void Convert_ShouldRateAMetricCylinderAtItsFillPressure_WhenConvertingToImperial()
    {
        // Arrange
        var plan = TestFactory.Plan();
        var fillPsi = Round(200 / BarPerPsi, 0);

        // Act
        var converted = PlanConverter.Convert(plan, UnitSystem.Imperial);

        // Assert
        var cylinder = Assert.Single(converted.Dives[0].Cylinders);
        Assert.Equal(fillPsi, cylinder.StartPressure, Precision);
        Assert.Equal(fillPsi, cylinder.RatedPressure, Precision);
        Assert.Equal(Round(12 * (fillPsi * BarPerPsi / BarPerAtmosphere) / LitersPerCubicFoot, 1), cylinder.Size, Precision);
    }

    [Fact]
    public void Convert_ShouldRoundDepthsRatesAndBreathingRatesToMetersAndLiters_WhenConvertingToMetric()
    {
        // Arrange
        var plan = DivePlanDocument.CreateNew(TestFactory.PlanId, "Plan", UnitSystem.Imperial, TestFactory.Now);

        // Act
        var converted = PlanConverter.Convert(plan, UnitSystem.Metric);

        // Assert
        Assert.Equal(UnitSystem.Metric, converted.Units);
        Assert.Equal(Round(60 * MetersPerFoot, 1), converted.Settings.DescentRate, Precision);
        Assert.Equal(Round(30 * MetersPerFoot, 1), converted.Settings.AscentRateDeep, Precision);
        Assert.Equal(Round(3 * MetersPerFoot, 1), converted.Settings.AscentRateShallow, Precision);
        Assert.Equal(Round(0.65 * LitersPerCubicFoot, 1), converted.Settings.BottomSac, Precision);
        Assert.Equal(Round(0.5 * LitersPerCubicFoot, 1), converted.Settings.DecoSac, Precision);
        Assert.Equal(Round(100 * MetersPerFoot, 1), Assert.Single(converted.Dives[0].Levels).Depth, Precision);
    }

    [Fact]
    public void Convert_ShouldGiveTheWaterVolumeAndFillPressureInBar_WhenConvertingAnImperialCylinderToMetric()
    {
        // Arrange
        var plan = DivePlanDocument.CreateNew(TestFactory.PlanId, "Plan", UnitSystem.Imperial, TestFactory.Now);

        // Act
        var converted = PlanConverter.Convert(plan, UnitSystem.Metric);

        // Assert
        var cylinder = Assert.Single(converted.Dives[0].Cylinders);
        Assert.Equal(Round(80 * LitersPerCubicFoot * BarPerAtmosphere / (3000 * BarPerPsi), 1), cylinder.Size, Precision);
        Assert.Equal(Round(3000 * BarPerPsi, 0), cylinder.StartPressure, Precision);
        Assert.Equal(0, cylinder.RatedPressure, Precision);
    }

    [Fact]
    public void Convert_ShouldKeepEveryValueThatHasNoUnitOrTheSameUnitInBothSystems_WhenConverting()
    {
        // Arrange
        var dive = TestFactory.Dive(surfaceIntervalMinutes: 45, circuit: new ClosedCircuitInput(1.2, 1.4), cylinders:
        [
            TestFactory.Cylinder(heliumPercent: 35, purpose: CylinderPurpose.Diluent),
            TestFactory.Cylinder(oxygenPercent: 100, size: 3, purpose: CylinderPurpose.Oxygen)
        ]);
        var plan = TestFactory.Plan(dive);

        // Act
        var converted = PlanConverter.Convert(plan, UnitSystem.Imperial);

        // Assert
        var expectedSettings = plan.Settings with
        {
            Altitude = converted.Settings.Altitude,
            DescentRate = converted.Settings.DescentRate,
            AscentRateDeep = converted.Settings.AscentRateDeep,
            AscentRateMiddle = converted.Settings.AscentRateMiddle,
            AscentRateToStops = converted.Settings.AscentRateToStops,
            AscentRateShallow = converted.Settings.AscentRateShallow,
            BottomSac = converted.Settings.BottomSac,
            DecoSac = converted.Settings.DecoSac,
            ContingencyExtraDepth = converted.Settings.ContingencyExtraDepth
        };
        Assert.Equal(expectedSettings, converted.Settings);
        Assert.Equal(plan.Algorithm, converted.Algorithm);
        var convertedDive = Assert.Single(converted.Dives);
        Assert.Equal(45, convertedDive.SurfaceIntervalMinutes, Precision);
        Assert.Equal(new ClosedCircuitInput(1.2, 1.4), convertedDive.Circuit);
        Assert.Equal(20, convertedDive.Levels[0].DurationMinutes, Precision);
        Assert.Equal([(21.0, 35.0, CylinderPurpose.Diluent), (100.0, 0.0, CylinderPurpose.Oxygen)],
            convertedDive.Cylinders.Select(static cylinder => (cylinder.OxygenPercent, cylinder.HeliumPercent, cylinder.Purpose)));
    }
}
