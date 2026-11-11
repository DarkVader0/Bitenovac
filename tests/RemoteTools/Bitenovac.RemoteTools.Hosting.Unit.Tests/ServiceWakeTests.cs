namespace Bitenovac.RemoteTools.Hosting.Unit.Tests;

public sealed class ServiceWakeTests
{
    [Fact]
    public void Request_ShouldCountAsOne_WhenAskedAgainBeforeTheWorkWaits()
    {
        // Arrange
        using var wake = new ServiceWake();
        wake.Request();

        // Act
        wake.Request();

        // Assert
        Assert.True(wake.WaitAsync(TestContext.Current.CancellationToken).IsCompletedSuccessfully);
        Assert.False(wake.WaitAsync(TestContext.Current.CancellationToken).IsCompleted);
    }
}
