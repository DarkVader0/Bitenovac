using Bitenovac.DecompressionAlgorithms.Core.Equipment;

namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed class CylinderSizesTests
{
    private const int Precision = 6;

    private static CylinderInput Al80(double startPressure) => new(21, 0, 80, 3000, startPressure, CylinderPurpose.BottomGas);

    [Fact]
    public void WithStartPressure_ShouldKeepTheSizeAndTheWaterVolume_WhenAnAl80IsFilledLower()
    {
        // Arrange
        var cylinder = Al80(3000);

        // Act
        var filled = CylinderSizes.WithStartPressure(cylinder, 2800);

        // Assert
        Assert.Equal(80, filled.Size, Precision);
        Assert.Equal(3000, filled.RatedPressure, Precision);
        Assert.Equal(2800, filled.StartPressure, Precision);
        Assert.Equal(UnitConversion.CylinderWaterVolume(cylinder, UnitSystem.Imperial).InLiter,
            UnitConversion.CylinderWaterVolume(filled, UnitSystem.Imperial).InLiter, Precision);
    }

    [Fact]
    public void WithWorkingPressure_ShouldKeepTheWaterVolume_WhenTheWorkingPressureChanges()
    {
        // Arrange
        var cylinder = Al80(3000);

        // Act
        var rerated = CylinderSizes.WithWorkingPressure(cylinder, 3300);

        // Assert
        Assert.Equal(80 * 3300 / 3000.0, rerated.Size, Precision);
        Assert.Equal(3300, rerated.RatedPressure, Precision);
        Assert.Equal(UnitConversion.CylinderWaterVolume(cylinder, UnitSystem.Imperial).InLiter,
            UnitConversion.CylinderWaterVolume(rerated, UnitSystem.Imperial).InLiter, Precision);
    }

    [Fact]
    public void WithWorkingPressure_ShouldKeepTheWorkingPressureAsGiven_WhenItIsNotPositive()
    {
        // Arrange
        var cylinder = Al80(3000);

        // Act
        var invalid = CylinderSizes.WithWorkingPressure(cylinder, 0);

        // Assert
        Assert.Equal(0, invalid.RatedPressure, Precision);
        Assert.Equal(80, invalid.Size, Precision);
    }

    [Fact]
    public void WithSize_ShouldChangeOnlyTheSize_WhenTheSizeIsGiven()
    {
        // Arrange
        var cylinder = Al80(2800);

        // Act
        var resized = CylinderSizes.WithSize(cylinder, 100);

        // Assert
        Assert.Equal(100, resized.Size, Precision);
        Assert.Equal(3000, resized.RatedPressure, Precision);
        Assert.Equal(2800, resized.StartPressure, Precision);
    }

    [Fact]
    public void WithStartPressure_ShouldThrowArgumentNullException_WhenTheCylinderIsNull()
    {
        // Arrange
        CylinderInput cylinder = null!;

        // Act
        Action act = () => CylinderSizes.WithStartPressure(cylinder, 200);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void WithWorkingPressure_ShouldMoveTheFillPressureWithIt_WhenTheFillFollowedTheWorkingPressure()
    {
        // Arrange
        var cylinder = Al80(3000);

        // Act
        var rerated = CylinderSizes.WithWorkingPressure(cylinder, 3300);

        // Assert
        Assert.Equal(3300, rerated.StartPressure, Precision);
    }

    [Fact]
    public void WithWorkingPressure_ShouldKeepTheFillPressure_WhenTheFillWasSetToSomethingElse()
    {
        // Arrange
        var cylinder = Al80(2800);

        // Act
        var rerated = CylinderSizes.WithWorkingPressure(cylinder, 3300);

        // Assert
        Assert.Equal(2800, rerated.StartPressure, Precision);
    }

    [Fact]
    public void WithWorkingPressure_ShouldSetTheWorkingPressureWithoutChangingTheSize_WhenThereWasNoWorkingPressure()
    {
        // Arrange
        var cylinder = new CylinderInput(21, 0, 80, 0, 2800, CylinderPurpose.BottomGas);

        // Act
        var rated = CylinderSizes.WithWorkingPressure(cylinder, 3000);

        // Assert
        Assert.Equal(80, rated.Size, Precision);
        Assert.Equal(3000, rated.RatedPressure, Precision);
        Assert.Equal(2800, rated.StartPressure, Precision);
    }
}
