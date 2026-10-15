using Bitenovac.RemoteBuildTool.Commands;
using Bitenovac.RemoteBuildTool.Service;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bitenovac.RemoteBuildTool.Service.Unit.Tests;

public sealed class WakeCommandTests
{
    [Fact]
    public void Run_ShouldFail_WhenTheServiceIsNotListening()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var settings = Settings(directory);
        var output = new CapturedOutput();

        // Act
        var exitCode = WakeCommand.Run(output.Pipeline, settings);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains($"the service is not listening at {directory.Combine("work/wake.sock")}", output.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_ShouldAskThePromotionForATurn_WhenTheServiceListens()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var settings = Settings(directory);
        var wake = new ServiceWake();
        using var listener = new WakeListener(HostSettings.Load(settings), wake, NullLogger<WakeListener>.Instance);
        await listener.StartAsync(TestContext.Current.CancellationToken);
        var output = new CapturedOutput();

        // Act
        var exitCode = WakeCommand.Run(output.Pipeline, settings);
        var woken = wake.WaitAsync(TestContext.Current.CancellationToken);
        await woken.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await listener.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(woken.IsCompletedSuccessfully);
        Assert.Equal($"Woke the service.{Environment.NewLine}", output.Out);
        Assert.True(listener.ExecuteTask!.IsCompletedSuccessfully);
    }

    /// <summary>
    /// Writes host settings whose work directory is under <paramref name="directory"/>.
    /// </summary>
    private static string Settings(TemporaryDirectory directory) =>
        TestFactory.WriteFile(directory.Combine("config.json"), $$"""
            {
              "repository": "owner/repo",
              "gitHubToken": "token",
              "workRoot": "{{directory.Combine("work").Replace(@"\", @"\\", StringComparison.Ordinal)}}"
            }
            """);
}
