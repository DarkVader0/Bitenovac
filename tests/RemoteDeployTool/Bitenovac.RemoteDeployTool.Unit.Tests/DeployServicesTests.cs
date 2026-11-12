namespace Bitenovac.RemoteDeployTool.Unit.Tests;

public sealed class DeployServicesTests
{
    [Fact]
    public void FeedFor_ShouldPushWithDotNetToTheOptionsFeed_WhenNothingElseIsGiven()
    {
        // Arrange
        var services = new DeployServices();

        // Act
        var feed = services.FeedFor(TestFactory.Options());

        // Assert
        Assert.IsType<DotNetPackageFeed>(feed);
    }

    [Fact]
    public void Run_ShouldRunTheProgramAndReturnWhatItSaid_WhenNothingElseIsGiven()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var services = new DeployServices();

        // Act
        var (exitCode, output) = services.Run("dotnet", ["--version"], directory.Path);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Matches(@"^\d+\.\d+\.\d+", output.Trim());
    }

    [Fact]
    public void Time_ShouldBeTheSystemClock_WhenNothingElseIsGiven()
    {
        // Arrange

        // Act
        var time = new DeployServices().Time;

        // Assert
        Assert.Same(TimeProvider.System, time);
    }
}
