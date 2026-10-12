using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionAlgorithms.Core.Unit.Tests;

public sealed class StopNotClearedExceptionTests
{
    private const int Precision = 9;

    [Fact]
    public void Constructor_ShouldKeepTheStopDepthAndLimit_WhenCreated()
    {
        // Arrange
        var limit = TimeSpan.FromHours(24);

        // Act
        var exception = new StopNotClearedException(Depth.FromMeter(21), limit);

        // Assert
        Assert.Equal(21, exception.StopDepth.InMeter, Precision);
        Assert.Equal(limit, exception.Limit);
    }

    [Fact]
    public void Message_ShouldStateTheDepthInMeters_WhenCreated()
    {
        // Arrange
        var stopDepth = Depth.FromMeter(21);

        // Act
        var exception = new StopNotClearedException(stopDepth, TimeSpan.FromHours(24));

        // Assert
        Assert.Equal(
            "The decompression stop at 21 m does not clear within 24 hours; " +
            "the gradient factors and available gases cannot decompress this dive.",
            exception.Message);
    }
}
