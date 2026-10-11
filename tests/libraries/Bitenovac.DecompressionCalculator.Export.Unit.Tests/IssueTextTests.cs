using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Export.Unit.Tests;

public sealed class IssueTextTests
{
    [Theory]
    [InlineData("Settings.DescentRate", "Settings: Problem.")]
    [InlineData("Algorithm.GradientFactorLow", "Algorithm: Problem.")]
    [InlineData("Dives[0].Levels[1].Depth", "Level 2: Problem.")]
    [InlineData("Dives[2].Cylinders[0].Size", "Cylinder 1: Problem.")]
    [InlineData("Dives[0].Circuit.BottomSetpoint", "Problem.")]
    [InlineData("Dives[0].Levels[x].Depth", "Problem.")]
    [InlineData("Dives[0].Levels[1", "Problem.")]
    [InlineData("Dives", "Problem.")]
    public void Describe_ShouldPutThePlaceOfTheIssueInFront_WhenThePathNamesOne(string path, string expected)
    {
        // Arrange
        var issue = new ValidationIssue(path, "Problem.");

        // Act
        var text = IssueText.Describe(issue);

        // Assert
        Assert.Equal(expected, text);
    }

    [Theory]
    [InlineData("Dives[1].Cylinders[0].Size", "Dive 2 · Cylinder 1: Problem.")]
    [InlineData("Dives[0].SurfaceIntervalMinutes", "Dive 1 · Problem.")]
    [InlineData("Settings.DescentRate", "Settings: Problem.")]
    public void DescribeWithDive_ShouldPutTheDiveInFront_WhenTheIssueBelongsToADive(string path, string expected)
    {
        // Arrange
        var issue = new ValidationIssue(path, "Problem.");

        // Act
        var text = IssueText.DescribeWithDive(issue);

        // Assert
        Assert.Equal(expected, text);
    }

    [Fact]
    public void Describe_ShouldThrowArgumentNullException_WhenTheIssueIsNull()
    {
        // Arrange
        ValidationIssue issue = null!;

        // Act
        Action act = () => IssueText.Describe(issue);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void DescribeWithDive_ShouldThrowArgumentNullException_WhenTheIssueIsNull()
    {
        // Arrange
        ValidationIssue issue = null!;

        // Act
        Action act = () => IssueText.DescribeWithDive(issue);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }
}
