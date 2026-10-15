using System.Globalization;
using Bitenovac.RemoteBuildTool.MsBuild;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class CoverageMinimumTests
{
    private const int Precision = 6;

    [Fact]
    public void Parse_ShouldBeNull_WhenTheProjectSetsNoMinimum()
    {
        // Arrange
        const string value = "";

        // Act
        var minimum = CoverageMinimum.Parse(value, "src/A/A.csproj", "MinimumLineCoverage");

        // Assert
        Assert.Null(minimum);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("95.5", 95.5)]
    [InlineData("100", 100)]
    public void Parse_ShouldReadThePercentageWithAPeriod_WhenTheMachinesCultureUsesAComma(string value, double expected)
    {
        // Arrange
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

        // Act
        double? minimum;
        try
        {
            minimum = CoverageMinimum.Parse(value, "src/A/A.csproj", "MinimumLineCoverage");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }

        // Assert
        Assert.NotNull(minimum);
        Assert.Equal(expected, minimum.Value, Precision);
    }

    [Theory]
    [InlineData("high")]
    [InlineData("-1")]
    [InlineData("100.5")]
    public void Parse_ShouldThrowNamingTheProject_WhenTheValueIsNotAPercentage(string value)
    {
        // Arrange

        // Act
        Action act = () => CoverageMinimum.Parse(value, "src/A/A.csproj", "MinimumBranchCoverage");

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Equal($"src/A/A.csproj: MinimumBranchCoverage must be a percentage from 0 to 100, such as 95.5; it is '{value}'.", exception.Message);
    }
}
