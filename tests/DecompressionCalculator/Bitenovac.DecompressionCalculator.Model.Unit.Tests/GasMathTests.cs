using Bitenovac.DecompressionAlgorithms.Core.Planning;

namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed class GasMathTests
{
    private const int Precision = 6;
    private const double SurfaceMillibar = 1013.25;
    private const double MillibarPerMeter = 1030 * 9.80665 / 100;
    private const double MetersPerFoot = 0.3048;

    private static readonly PlannerSettings Metric = PlannerSettings.Default(UnitSystem.Metric);

    private static double Ambient(double meters) => SurfaceMillibar + meters * MillibarPerMeter;

    private static double DepthOf(double millibar) => (millibar - SurfaceMillibar) / MillibarPerMeter;

    [Fact]
    public void MaximumOperatingDepth_ShouldBeWhereTheOxygenReachesTheLimit_WhenTheDepthIsRealistic()
    {
        // Arrange
        var expected = DepthOf(1600 / 0.5);

        // Act
        var depth = GasMath.MaximumOperatingDepth(50, 1.6, Metric, UnitSystem.Metric);

        // Assert
        Assert.Equal(expected, depth, Precision);
    }

    [Theory]
    [InlineData(UnitSystem.Metric, 22)]
    [InlineData(UnitSystem.Imperial, 22 / MetersPerFoot)]
    public void MaximumOperatingDepth_ShouldBeTenMetersPerBarAboveOneBar_WhenTheDepthIsSimplified(UnitSystem units, double expected)
    {
        // Arrange
        var settings = PlannerSettings.Default(units) with { MaximumOperatingDepthModel = MaximumOperatingDepthModel.Simplified };

        // Act
        var depth = GasMath.MaximumOperatingDepth(50, 1.6, settings, units);

        // Assert
        Assert.Equal(expected, depth, Precision);
    }

    [Theory]
    [InlineData(50, 0)]
    [InlineData(0, 1.4)]
    [InlineData(101, 1.4)]
    public void MaximumOperatingDepth_ShouldThrowArgumentOutOfRangeException_WhenTheGasOrTheLimitIsNotValid(double percentO2, double maxPo2)
    {
        // Arrange

        // Act
        Action act = () => GasMath.MaximumOperatingDepth(percentO2, maxPo2, Metric, UnitSystem.Metric);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Theory]
    [InlineData(true, 0.65, 1.0)]
    [InlineData(false, 0.44, 0.79)]
    public void EquivalentNarcoticDepth_ShouldMatchTheNarcoticPressureOfAir_WhenOxygenCountsOrNot(bool oxygenNarcotic, double narcotic, double air)
    {
        // Arrange
        var settings = Metric with { OxygenNarcotic = oxygenNarcotic };
        var expected = DepthOf(Ambient(45) * narcotic / air);

        // Act
        var depth = GasMath.EquivalentNarcoticDepth(21, 35, 45, settings, UnitSystem.Metric);

        // Assert
        Assert.Equal(expected, depth, Precision);
    }

    [Fact]
    public void EquivalentNarcoticDepth_ShouldBeTheSurface_WhenTheGasIsLessNarcoticThanAirAtTheSurface()
    {
        // Arrange

        // Act
        var depth = GasMath.EquivalentNarcoticDepth(10, 70, 3, Metric, UnitSystem.Metric);

        // Assert
        Assert.Equal(0, depth, Precision);
    }

    [Fact]
    public void EquivalentNarcoticDepth_ShouldThrowArgumentOutOfRangeException_WhenTheDepthIsNegative()
    {
        // Arrange

        // Act
        Action act = () => GasMath.EquivalentNarcoticDepth(21, 0, -1, Metric, UnitSystem.Metric);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void Density_ShouldWeighEachGasByItsFractionAndScaleItByTheAmbientPressure_WhenAGasIsBreathedAtDepth()
    {
        // Arrange
        var expected = (0.18 * GasMath.OxygenDensity + 0.37 * GasMath.NitrogenDensity + 0.45 * GasMath.HeliumDensity) * Ambient(60) / SurfaceMillibar;

        // Act
        var density = GasMath.Density(18, 45, 60, Metric, UnitSystem.Metric);

        // Assert
        Assert.Equal(expected, density, Precision);
    }

    [Fact]
    public void Density_ShouldThrowArgumentOutOfRangeException_WhenTheDepthIsNegative()
    {
        // Arrange

        // Act
        Action act = () => GasMath.Density(21, 0, -1, Metric, UnitSystem.Metric);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Theory]
    [InlineData(true, 19, 43)]
    [InlineData(false, 19, 36)]
    public void BestMix_ShouldRoundOxygenDownAndHeliumUp_WhenTheDepthNeedsHelium(bool oxygenNarcotic, int expectedO2, int expectedHe)
    {
        // Arrange
        var settings = Metric with { OxygenNarcotic = oxygenNarcotic };

        // Act
        var mix = GasMath.BestMix(60, 1.4, 30, settings, UnitSystem.Metric);

        // Assert
        Assert.Equal(new BestMix(expectedO2, expectedHe), mix);
        Assert.Equal(expectedO2, (int)Math.Floor(1400 / Ambient(60) * 100));
    }

    [Theory]
    [InlineData(20, 46)]
    [InlineData(0, 100)]
    public void BestMix_ShouldNeedNoHelium_WhenTheDepthIsWithinTheNarcoticLimit(double depth, int expectedO2)
    {
        // Arrange

        // Act
        var mix = GasMath.BestMix(depth, 1.4, 30, Metric, UnitSystem.Metric);

        // Assert
        Assert.Equal(new BestMix(expectedO2, 0), mix);
    }

    [Theory]
    [InlineData(-1, 1.4, 30)]
    [InlineData(60, 0, 30)]
    [InlineData(60, 1.4, -1)]
    public void BestMix_ShouldThrowArgumentOutOfRangeException_WhenAnInputIsNotValid(double depth, double maxPo2, double maximumEnd)
    {
        // Arrange

        // Act
        Action act = () => GasMath.BestMix(depth, maxPo2, maximumEnd, Metric, UnitSystem.Metric);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void Calculations_ShouldThrowArgumentNullException_WhenTheSettingsAreNull()
    {
        // Arrange
        PlannerSettings settings = null!;

        // Act
        Action[] acts =
        [
            () => GasMath.MaximumOperatingDepth(50, 1.6, settings, UnitSystem.Metric),
            () => GasMath.EquivalentNarcoticDepth(21, 0, 30, settings, UnitSystem.Metric),
            () => GasMath.Density(21, 0, 30, settings, UnitSystem.Metric),
            () => GasMath.BestMix(30, 1.4, 30, settings, UnitSystem.Metric)
        ];

        // Assert
        Assert.All(acts, static act => Assert.Throws<ArgumentNullException>(act));
    }
}
