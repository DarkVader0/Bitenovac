using Bitenovac.DecompressionAlgorithms.Core.Environment;
using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed class UnitConversionTests
{
    private const int Precision = 9;
    private const double MetersPerFoot = 0.3048;
    private const double BarPerPsi = 0.06894757293168;
    private const double LitersPerCubicFoot = 28.316846592;
    private const double BarPerAtmosphere = 1.01325;

    [Fact]
    public void ToDepth_ShouldReadMeters_WhenTheUnitsAreMetric()
    {
        // Arrange

        // Act
        var depth = UnitConversion.ToDepth(30, UnitSystem.Metric);

        // Assert
        Assert.Equal(30, depth.InMeter, Precision);
    }

    [Fact]
    public void ToDepth_ShouldReadFeet_WhenTheUnitsAreImperial()
    {
        // Arrange

        // Act
        var depth = UnitConversion.ToDepth(100, UnitSystem.Imperial);

        // Assert
        Assert.Equal(100 * MetersPerFoot, depth.InMeter, Precision);
    }

    [Fact]
    public void FromDepth_ShouldReturnMeters_WhenTheUnitsAreMetric()
    {
        // Arrange
        var depth = Depth.FromMeter(42);

        // Act
        var value = UnitConversion.FromDepth(depth, UnitSystem.Metric);

        // Assert
        Assert.Equal(42, value, Precision);
    }

    [Fact]
    public void FromDepth_ShouldReturnFeet_WhenTheUnitsAreImperial()
    {
        // Arrange
        var depth = Depth.FromMeter(30.48);

        // Act
        var value = UnitConversion.FromDepth(depth, UnitSystem.Imperial);

        // Assert
        Assert.Equal(100, value, Precision);
    }

    [Fact]
    public void ToPressure_ShouldReadBar_WhenTheUnitsAreMetric()
    {
        // Arrange

        // Act
        var pressure = UnitConversion.ToPressure(200, UnitSystem.Metric);

        // Assert
        Assert.Equal(200, pressure.InBar, Precision);
    }

    [Fact]
    public void ToPressure_ShouldReadPsi_WhenTheUnitsAreImperial()
    {
        // Arrange

        // Act
        var pressure = UnitConversion.ToPressure(3000, UnitSystem.Imperial);

        // Assert
        Assert.Equal(3000 * BarPerPsi, pressure.InBar, Precision);
    }

    [Fact]
    public void FromPressure_ShouldReturnBar_WhenTheUnitsAreMetric()
    {
        // Arrange
        var pressure = Pressure.FromBar(232);

        // Act
        var value = UnitConversion.FromPressure(pressure, UnitSystem.Metric);

        // Assert
        Assert.Equal(232, value, Precision);
    }

    [Fact]
    public void FromPressure_ShouldReturnPsi_WhenTheUnitsAreImperial()
    {
        // Arrange
        var pressure = Pressure.FromBar(200);

        // Act
        var value = UnitConversion.FromPressure(pressure, UnitSystem.Imperial);

        // Assert
        Assert.Equal(200 / BarPerPsi, value, Precision);
    }

    [Fact]
    public void ToLitersPerMinute_ShouldKeepLiters_WhenTheUnitsAreMetric()
    {
        // Arrange

        // Act
        var value = UnitConversion.ToLitersPerMinute(18, UnitSystem.Metric);

        // Assert
        Assert.Equal(18, value, Precision);
    }

    [Fact]
    public void ToLitersPerMinute_ShouldConvertCubicFeet_WhenTheUnitsAreImperial()
    {
        // Arrange

        // Act
        var value = UnitConversion.ToLitersPerMinute(0.65, UnitSystem.Imperial);

        // Assert
        Assert.Equal(0.65 * LitersPerCubicFoot, value, Precision);
    }

    [Fact]
    public void FromGasVolume_ShouldReturnLiters_WhenTheUnitsAreMetric()
    {
        // Arrange
        var volume = Volume.FromLiter(2400);

        // Act
        var value = UnitConversion.FromGasVolume(volume, UnitSystem.Metric);

        // Assert
        Assert.Equal(2400, value, Precision);
    }

    [Fact]
    public void FromGasVolume_ShouldReturnCubicFeet_WhenTheUnitsAreImperial()
    {
        // Arrange
        var volume = Volume.FromLiter(LitersPerCubicFoot * 80);

        // Act
        var value = UnitConversion.FromGasVolume(volume, UnitSystem.Imperial);

        // Assert
        Assert.Equal(80, value, Precision);
    }

    [Fact]
    public void CylinderWaterVolume_ShouldBeTheEnteredSize_WhenTheUnitsAreMetric()
    {
        // Arrange
        var cylinder = TestFactory.Cylinder(size: 12);

        // Act
        var volume = UnitConversion.CylinderWaterVolume(cylinder, UnitSystem.Metric);

        // Assert
        Assert.Equal(12, volume.InLiter, Precision);
    }

    [Fact]
    public void CylinderWaterVolume_ShouldFollowBoylesLaw_WhenTheUnitsAreImperial()
    {
        // Arrange
        var cylinder = TestFactory.Cylinder(size: 77.4, ratedPressure: 3000, startPressure: 3000);

        // Act
        var volume = UnitConversion.CylinderWaterVolume(cylinder, UnitSystem.Imperial);

        // Assert
        Assert.Equal(77.4 * LitersPerCubicFoot * BarPerAtmosphere / (3000 * BarPerPsi), volume.InLiter, Precision);
    }

    [Fact]
    public void SurfacePressure_ShouldBeOneStandardAtmosphere_WhenTheSiteIsAtSeaLevel()
    {
        // Arrange

        // Act
        var pressure = UnitConversion.SurfacePressure(0, UnitSystem.Metric);

        // Assert
        Assert.Equal(BarPerAtmosphere, pressure.InBar, Precision);
    }

    [Fact]
    public void SurfacePressure_ShouldReadTheAltitudeInFeet_WhenTheUnitsAreImperial()
    {
        // Arrange
        var expected = PhysicalConstants.AtmosphericPressureAtAltitudeMillibar(1000 * MetersPerFoot);

        // Act
        var pressure = UnitConversion.SurfacePressure(1000, UnitSystem.Imperial);

        // Assert
        Assert.Equal(expected, pressure.InMillibar, Precision);
    }
}
