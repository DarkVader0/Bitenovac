using Bitenovac.RemoteBuildTool.Core.Graph;

namespace Bitenovac.RemoteBuildTool.Core.Unit.Tests;

public sealed class ProjectIdTests
{
    [Fact]
    public void Constructor_ShouldNormaliseBackslashesToForwardSlashes_WhenPathUsesWindowsSeparators()
    {
        // Arrange
        const string windowsPath = @"src\Foo\Foo.csproj";

        // Act
        var id = new ProjectId(windowsPath);

        // Assert
        Assert.Equal("src/Foo/Foo.csproj", id.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrow_WhenValueIsEmptyOrWhiteSpace(string value)
    {
        // Arrange

        // Act
        var act = () => new ProjectId(value);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Equality_ShouldTreatTwoIdsAsEqual_WhenTheyNormaliseToTheSamePath()
    {
        // Arrange
        var fromForwardSlash = new ProjectId("src/Foo/Foo.csproj");
        var fromBackslash = new ProjectId(@"src\Foo\Foo.csproj");

        // Act
        var areEqual = fromForwardSlash == fromBackslash;

        // Assert
        Assert.True(areEqual);
    }

    [Fact]
    public void CompareTo_ShouldOrderOrdinally_WhenComparingTwoIds()
    {
        // Arrange
        var a = new ProjectId("a/A.csproj");
        var b = new ProjectId("b/B.csproj");

        // Act
        var result = a.CompareTo(b);

        // Assert
        Assert.True(result < 0);
    }

    [Fact]
    public void CompareTo_ShouldSortAfterNull_WhenOtherIsNull()
    {
        // Arrange
        var a = new ProjectId("a/A.csproj");

        // Act
        var result = a.CompareTo(null);

        // Assert
        Assert.True(result > 0);
    }

    [Theory]
    [InlineData("a/A.csproj", "b/B.csproj", true, true, false, false)]
    [InlineData("b/B.csproj", "a/A.csproj", false, false, true, true)]
    [InlineData("a/A.csproj", "a/A.csproj", false, true, false, true)]
    [InlineData(null, "a/A.csproj", true, true, false, false)]
    [InlineData("a/A.csproj", null, false, false, true, true)]
    [InlineData(null, null, false, true, false, true)]
    public void Operators_ShouldOrderOrdinallyWithAMissingIdFirst_WhenComparingTwoIds(
        string? left,
        string? right,
        bool less,
        bool lessOrEqual,
        bool greater,
        bool greaterOrEqual)
    {
        // Arrange
        var a = left is null ? null : new ProjectId(left);
        var b = right is null ? null : new ProjectId(right);

        // Act
        var results = (a < b, a <= b, a > b, a >= b);

        // Assert
        Assert.Equal((less, lessOrEqual, greater, greaterOrEqual), results);
    }
}
