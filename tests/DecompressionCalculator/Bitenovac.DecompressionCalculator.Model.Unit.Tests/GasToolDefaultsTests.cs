using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed class GasToolDefaultsTests
{
    private const int Precision = 6;

    [Fact]
    public void For_ShouldUseMetricDepths_WhenTheUnitsAreMetric()
    {
        // Arrange
        var settings = PlannerSettings.Default(UnitSystem.Metric);

        // Act
        var defaults = GasToolDefaults.For(settings, UnitSystem.Metric);

        // Assert
        Assert.Equal(45, defaults.EndDepth, Precision);
        Assert.Equal(60, defaults.MixDepth, Precision);
        Assert.Equal(30, defaults.MixEnd, Precision);
    }

    [Fact]
    public void For_ShouldUseImperialDepths_WhenTheUnitsAreImperial()
    {
        // Arrange
        var settings = PlannerSettings.Default(UnitSystem.Imperial);

        // Act
        var defaults = GasToolDefaults.For(settings, UnitSystem.Imperial);

        // Assert
        Assert.Equal(150, defaults.EndDepth, Precision);
        Assert.Equal(200, defaults.MixDepth, Precision);
        Assert.Equal(100, defaults.MixEnd, Precision);
    }

    [Fact]
    public void For_ShouldUseTheSettingsOxygenLimits_WhenTheSettingsHaveThem()
    {
        // Arrange
        var settings = PlannerSettings.Default(UnitSystem.Metric) with { DecoPo2 = 1.5, BottomPo2 = 1.3 };

        // Act
        var defaults = GasToolDefaults.For(settings, UnitSystem.Metric);

        // Assert
        Assert.Equal(1.5, defaults.ModPo2, Precision);
        Assert.Equal(1.3, defaults.MixPo2, Precision);
    }

    [Fact]
    public void For_ShouldThrowArgumentNullException_WhenTheSettingsAreNull()
    {
        // Arrange

        // Act
        Action act = () => GasToolDefaults.For(null!, UnitSystem.Metric);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }
}
