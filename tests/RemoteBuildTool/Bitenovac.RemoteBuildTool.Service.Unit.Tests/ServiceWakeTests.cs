using Bitenovac.RemoteBuildTool.Service;

namespace Bitenovac.RemoteBuildTool.Service.Unit.Tests;

public sealed class ServiceWakeTests
{
    [Fact]
    public void Request_ShouldCountAsOne_WhenAskedAgainBeforeTheWorkWaits()
    {
        // Arrange
        var wake = new ServiceWake();
        wake.Request();

        // Act
        wake.Request();

        // Assert
        Assert.True(wake.WaitAsync(TestContext.Current.CancellationToken).IsCompletedSuccessfully);
        Assert.False(wake.WaitAsync(TestContext.Current.CancellationToken).IsCompleted);
    }
}
