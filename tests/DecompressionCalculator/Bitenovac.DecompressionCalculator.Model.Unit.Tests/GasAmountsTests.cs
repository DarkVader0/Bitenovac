using Bitenovac.DecompressionAlgorithms.Core.Equipment;

namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed class GasAmountsTests
{
    private const int Precision = 6;
    private const double MbarPerPsi = 68.94757293168;
    private const double MlPerCubicFoot = 28316.846592;

    private static CylinderRow Cylinder() => new("Air", CylinderPurpose.BottomGas, 1800, 200, 50, false, 600, 600, true)
    {
        StartBar = 200,
        EndBar = 50,
        WaterLitres = 12,
        UsedLitres = 1800,
        ReserveLitres = 600,
        LeftLitres = 600,
        MissingLitres = 0
    };

    [Fact]
    public void For_ShouldShowEveryFigureAsBar_WhenPressureIsChosenInMetric()
    {
        // Arrange
        var cylinder = Cylinder();

        // Act
        var amount = GasAmounts.For(cylinder, GasQuantity.Pressure, UnitSystem.Metric);

        // Assert
        Assert.Equal(200, amount.Start, Precision);
        Assert.Equal(50, amount.End, Precision);
        Assert.Equal(150, amount.Used, Precision);
        Assert.Equal(50, amount.Reserve, Precision);
        Assert.Equal(50, amount.Left, Precision);
    }

    [Fact]
    public void For_ShouldShowEveryFigureAsLiters_WhenVolumeIsChosenInMetric()
    {
        // Arrange
        var cylinder = Cylinder();

        // Act
        var amount = GasAmounts.For(cylinder, GasQuantity.Volume, UnitSystem.Metric);

        // Assert
        Assert.Equal(2400, amount.Start, Precision);
        Assert.Equal(600, amount.End, Precision);
        Assert.Equal(1800, amount.Used, Precision);
        Assert.Equal(600, amount.Reserve, Precision);
        Assert.Equal(600, amount.Left, Precision);
    }

    [Fact]
    public void For_ShouldShowPsiAndCubicFeet_WhenTheUnitsAreImperial()
    {
        // Arrange
        var cylinder = Cylinder();

        // Act
        var pressure = GasAmounts.For(cylinder, GasQuantity.Pressure, UnitSystem.Imperial);
        var volume = GasAmounts.For(cylinder, GasQuantity.Volume, UnitSystem.Imperial);

        // Assert
        Assert.Equal(200 * 1000 / MbarPerPsi, pressure.Start, Precision);
        Assert.Equal(150 * 1000 / MbarPerPsi, pressure.Used, Precision);
        Assert.Equal(2400 / (MlPerCubicFoot / 1000), volume.Start, Precision);
        Assert.Equal(1800 / (MlPerCubicFoot / 1000), volume.Used, Precision);
    }

    [Fact]
    public void For_ShouldShowTheMissingGasInTheChosenUnit_WhenACylinderRunsOut()
    {
        // Arrange
        var runsOut = new CylinderRow("Air", CylinderPurpose.BottomGas, 2000, 200, 0, false, 600, 0, false)
        {
            StartBar = 200,
            EndBar = 0,
            WaterLitres = 12,
            UsedLitres = 2000,
            ReserveLitres = 600,
            LeftLitres = 0,
            MissingLitres = 400
        };

        // Act
        var pressure = GasAmounts.For(runsOut, GasQuantity.Pressure, UnitSystem.Metric);
        var volume = GasAmounts.For(runsOut, GasQuantity.Volume, UnitSystem.Metric);

        // Assert
        Assert.Equal(400.0 / 12, pressure.Missing, Precision);
        Assert.Equal(400, volume.Missing, Precision);
    }

    [Fact]
    public void For_ShouldThrowArgumentNullException_WhenTheCylinderIsNull()
    {
        // Arrange
        CylinderRow cylinder = null!;

        // Act
        Action act = () => GasAmounts.For(cylinder, GasQuantity.Pressure, UnitSystem.Metric);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }
}
