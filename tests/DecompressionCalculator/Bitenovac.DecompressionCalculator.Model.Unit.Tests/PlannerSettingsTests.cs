using Bitenovac.DecompressionAlgorithms.Core.Environment;
using Bitenovac.DecompressionAlgorithms.Core.Planning;

namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed class PlannerSettingsTests
{
    [Fact]
    public void Default_ShouldReturnSeaLevelSaltWaterMetricSettings_WhenTheUnitsAreMetric()
    {
        // Arrange
        var expected = new PlannerSettings(0, Salinity.Salt, 18, 9, 9, 9, 1, 18, 14, 1, 0.6, 6, 1.4, 1.6,
            MaximumOperatingDepthModel.Realistic, 2, 2, 1, 2, 20, 5, false, false, false, false);

        // Act
        var settings = PlannerSettings.Default(UnitSystem.Metric);

        // Assert
        Assert.Equal(expected, settings);
    }

    [Fact]
    public void Default_ShouldUseFeetAndCubicFeetForRatesAndConsumption_WhenTheUnitsAreImperial()
    {
        // Arrange
        var expected = PlannerSettings.Default(UnitSystem.Metric) with
        {
            DescentRate = 60,
            AscentRateDeep = 30,
            AscentRateMiddle = 30,
            AscentRateToStops = 30,
            AscentRateShallow = 3,
            BottomSac = 0.65,
            DecoSac = 0.5,
            ContingencyExtraDepth = 10
        };

        // Act
        var settings = PlannerSettings.Default(UnitSystem.Imperial);

        // Assert
        Assert.Equal(expected, settings);
    }

    [Fact]
    public void Default_ShouldNotMergeAnyStops_WhenNothingWasChosen()
    {
        // Arrange

        // Act
        var settings = PlannerSettings.Default(UnitSystem.Metric);

        // Assert
        Assert.False(settings.MergeAscentsIntoStops);
        Assert.False(settings.MergeDescentsIntoStops);
    }
}
