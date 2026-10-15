using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed class FailureMessagesTests
{
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
