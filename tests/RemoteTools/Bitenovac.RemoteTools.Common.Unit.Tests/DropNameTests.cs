namespace Bitenovac.RemoteTools.Common.Unit.Tests;

public sealed class DropNameTests
{
    private static readonly DateOnly Today = new(2026, 10, 7);

    [Fact]
    public void Next_ShouldStartAtOne_WhenTheDropRootDoesNotExist()
    {
        // Arrange
        using var directory = TestFactory.Directory();

        // Act
        var version = DropName.Next(directory.Combine("missing"), Today);

        // Assert
        Assert.Equal("2026.10.07.001", version);
    }

    [Fact]
    public void Next_ShouldStartAtOne_WhenOnlyOtherDatesWereDropped()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        Directory.CreateDirectory(directory.Combine("2026.10.06.004"));

        // Act
        var version = DropName.Next(directory.Path, Today);

        // Assert
        Assert.Equal("2026.10.07.001", version);
    }

    [Fact]
    public void Next_ShouldFollowTheHighestCounter_WhenTheDateWasAlreadyDropped()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        Directory.CreateDirectory(directory.Combine("2026.10.07.001"));
        Directory.CreateDirectory(directory.Combine("2026.10.07.009"));
        Directory.CreateDirectory(directory.Combine("2026.10.07.002"));

        // Act
        var version = DropName.Next(directory.Path, Today);

        // Assert
        Assert.Equal("2026.10.07.010", version);
    }

    [Theory]
    [InlineData(".staging-2026.10.07.005")]
    [InlineData("2026.10.07.latest")]
    [InlineData("2026.10.07.-3")]
    public void Next_ShouldIgnoreTheDirectory_WhenItIsNotANumberedDrop(string name)
    {
        // Arrange
        using var directory = TestFactory.Directory();
        Directory.CreateDirectory(directory.Combine(name));

        // Act
        var version = DropName.Next(directory.Path, Today);

        // Assert
        Assert.Equal("2026.10.07.001", version);
    }

    [Fact]
    public void Next_ShouldIgnoreFiles_WhenTheyAreNamedLikeADrop()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestFactory.WriteFile(directory.Combine("2026.10.07.003"), "not a drop");

        // Act
        var version = DropName.Next(directory.Path, Today);

        // Assert
        Assert.Equal("2026.10.07.001", version);
    }

    [Theory]
    [InlineData("2026.10.07.001", true)]
    [InlineData("2026.10.07.1", false)]
    [InlineData("2026.10.07.latest", false)]
    [InlineData(".staging-2026.10.07.005", false)]
    [InlineData("2026.10.07.001.bak", false)]
    [InlineData("2026.10.07.٠٠١", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsDropName_ShouldAcceptOnlyADateAndAThreeDigitCounter_WhenGivenAName(string? name, bool expected)
    {
        // Arrange

        // Act
        var isDropName = DropName.IsDropName(name);

        // Assert
        Assert.Equal(expected, isDropName);
    }

    [Fact]
    public void Newest_ShouldReturnNothing_WhenTheDropRootDoesNotExist()
    {
        // Arrange
        using var directory = TestFactory.Directory();

        // Act
        var newest = DropName.Newest(directory.Combine("missing"));

        // Assert
        Assert.Null(newest);
    }

    [Fact]
    public void Newest_ShouldReturnNothing_WhenNothingInTheDropRootIsADrop()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        Directory.CreateDirectory(directory.Combine(".staging-2026.10.07.005"));
        TestFactory.WriteFile(directory.Combine("2026.10.07.003"), "a file, not a drop");

        // Act
        var newest = DropName.Newest(directory.Path);

        // Assert
        Assert.Null(newest);
    }

    [Fact]
    public void Newest_ShouldReturnTheLatestDate_ThenTheHighestCounter_WhenSeveralDropsExist()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        Directory.CreateDirectory(directory.Combine("2026.10.07.009"));
        Directory.CreateDirectory(directory.Combine("2026.10.08.001"));
        Directory.CreateDirectory(directory.Combine("2026.10.08.002"));
        Directory.CreateDirectory(directory.Combine(".staging-2026.10.08.003"));

        // Act
        var newest = DropName.Newest(directory.Path);

        // Assert
        Assert.Equal("2026.10.08.002", newest);
    }
}
