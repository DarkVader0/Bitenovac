namespace Bitenovac.DecompressionCalculator.Export.Unit.Tests;

public sealed class FormatsTests
{
    [Theory]
    [InlineData(30.4, 0, "30")]
    [InlineData(1.46, 1, "1.5")]
    [InlineData(1234.5678, 2, "1234.57")]
    public void Number_ShouldRoundToTheDecimalsWithAPoint_WhenFormatting(double value, int decimals, string expected)
    {
        // Arrange

        // Act
        var text = Formats.Number(value, decimals);

        // Assert
        Assert.Equal(expected, text);
    }

    [Theory]
    [InlineData(1200, "20")]
    [InlineData(100, "1:40")]
    [InlineData(20, "0:20")]
    [InlineData(3599.6, "60")]
    public void Clock_ShouldShowWholeMinutesOrMinutesAndSeconds_WhenFormatting(double seconds, string expected)
    {
        // Arrange
        var duration = TimeSpan.FromSeconds(seconds);

        // Act
        var text = Formats.Clock(duration);

        // Assert
        Assert.Equal(expected, text);
    }

    [Fact]
    public void Minutes_ShouldAddTheMinuteUnitToTheClock_WhenFormatting()
    {
        // Arrange
        var duration = TimeSpan.FromSeconds(100);

        // Act
        var text = Formats.Minutes(duration);

        // Assert
        Assert.Equal("1:40 min", text);
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(45, "0:45")]
    [InlineData(60, "1:00")]
    [InlineData(90, "1:30")]
    [InlineData(1510, "25:10")]
    [InlineData(89.5, "1:30")]
    public void HoursAndMinutes_ShouldGiveHoursAndTwoDigitMinutes_WhenTheDurationIsGiven(double minutes, string expected)
    {
        // Arrange
        var duration = TimeSpan.FromMinutes(minutes);

        // Act
        var text = Formats.HoursAndMinutes(duration);

        // Assert
        Assert.Equal(expected, text);
    }
}
