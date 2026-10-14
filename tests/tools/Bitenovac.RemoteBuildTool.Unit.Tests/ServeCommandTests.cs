using Bitenovac.RemoteBuildTool.Commands;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class ServeCommandTests
{
    [Fact]
    public void Run_ShouldStartAndStopTheService_WhenItIsCancelled()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var settings = TestFactory.WriteFile(directory.Combine("config.json"), $$"""
            {
              "repository": "owner/repo",
              "gitHubToken": "token",
              "gitHubApiUrl": "http://127.0.0.1:9/",
              "cloneUrl": {{Json(directory.Combine("no-such-repository"))}},
              "pool": "docker",
              "machine": "service-host",
              "workRoot": {{Json(directory.Combine("work"))}},
              "mainStore": {{Json(directory.Combine("main"))}},
              "stagingRoot": {{Json(directory.Combine("staging"))}},
              "logsRoot": {{Json(directory.Combine("logs"))}},
              "telemetryRoot": {{Json(directory.Combine("telemetry"))}}
            }
            """);
        var output = new CapturedOutput();

        // Act
        var exitCode = ServeCommand.Run(output.Pipeline, settings, new CancellationToken(canceled: true));

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(
            $"Serving owner/repo as service-host in pool docker.{Environment.NewLine}Stopped.{Environment.NewLine}",
            output.Out);
    }

    private static string Json(string value) => $"\"{value.Replace(@"\", @"\\", StringComparison.Ordinal)}\"";
}
