using Bitenovac.DecompressionAlgorithms.Core.Calculations;
using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed class FailureMessagesTests
{
    private const double LitersPerCubicFoot = 28.316846592;

    [Fact]
    public void Describe_ShouldStateTheVolumesInLiters_WhenThePlanIsMetric()
    {
        // Arrange
        var exception = new InsufficientGasException(GasMixture.Air, Volume.FromLiter(2500), Volume.FromLiter(2400));

        // Act
        var message = FailureMessages.Describe(exception, UnitSystem.Metric);

        // Assert
        Assert.Equal(
            "The demand for gas exceeds what the matching cylinder holds: the profile draws 2500 L of Air " +
            "from a cylinder holding 2400 L.",
            message);
    }

    [Fact]
    public void Describe_ShouldStateTheVolumesInCubicFeet_WhenThePlanIsImperial()
    {
        // Arrange
        var exception = new InsufficientGasException(
            GasMixture.FromPercent(32, 0),
            Volume.FromLiter(90.5 * LitersPerCubicFoot),
            Volume.FromLiter(80 * LitersPerCubicFoot));

        // Act
        var message = FailureMessages.Describe(exception, UnitSystem.Imperial);

        // Assert
        Assert.Equal(
            "The demand for gas exceeds what the matching cylinder holds: the profile draws 90.5 cu ft of NX32 " +
            "from a cylinder holding 80 cu ft.",
            message);
    }

    [Fact]
    public void Describe_ShouldStateTheStopDepthInMeters_WhenThePlanIsMetric()
    {
        // Arrange
        var exception = new StopNotClearedException(Depth.FromMeter(21), TimeSpan.FromHours(24));

        // Act
        var message = FailureMessages.Describe(exception, UnitSystem.Metric);

        // Assert
        Assert.Equal(
            "The decompression stop at 21 m does not clear within 24 hours; " +
            "the gradient factors and available gases cannot decompress this dive.",
            message);
    }

    [Fact]
    public void Describe_ShouldStateTheStopDepthInFeet_WhenThePlanIsImperial()
    {
        // Arrange
        var exception = new StopNotClearedException(Depth.FromMeter(70 * 0.3048), TimeSpan.FromHours(24));

        // Act
        var message = FailureMessages.Describe(exception, UnitSystem.Imperial);

        // Assert
        Assert.Equal(
            "The decompression stop at 70 ft does not clear within 24 hours; " +
            "the gradient factors and available gases cannot decompress this dive.",
            message);
    }

    [Fact]
    public void Describe_ShouldKeepTheMessage_WhenTheFailureCarriesNoUnits()
    {
        // Arrange
        var exception = new InvalidOperationException("No breathable gas at the given depth.");

        // Act
        var message = FailureMessages.Describe(exception, UnitSystem.Imperial);

        // Assert
        Assert.Equal("No breathable gas at the given depth.", message);
    }
}
