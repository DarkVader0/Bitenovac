using System.Reflection;
using Bitenovac.RemoteBuildTool.Commands;
using Bitenovac.RemoteBuildTool.Hashing;

namespace Bitenovac.RemoteBuildTool.Service.Unit.Tests;

[Collection(EnvironmentVariables.Collection)]
public sealed class ServiceCommandLineTests : IDisposable
{
    private readonly EnvironmentVariables _environment = new();

    public void Dispose() => _environment.Dispose();

    [Fact]
    public void Run_ShouldPrintBothUsagesAndFail_WhenNoArgumentsAreGiven()
    {
        // Arrange
        var output = new CapturedOutput();

        // Act
        var exitCode = ServiceCommandLine.Run([], output.Pipeline);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("remotebuildtool-service serve", output.Out, StringComparison.Ordinal);
        Assert.Contains("remotebuildtool plan", output.Out, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Run_ShouldSucceed_WhenHelpIsRequested(string argument)
    {
        // Arrange
        var output = new CapturedOutput();

        // Act
        var exitCode = ServiceCommandLine.Run([argument], output.Pipeline);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("remotebuildtool-service wake", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldPrintTheServicesOwnVersion_WhenAnotherCommandAsksForIt()
    {
        // Arrange
        var service = typeof(ServiceCommandLine).Assembly;
        var expected = ToolVersion.Of(service.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion, service.GetName().Version);
        var output = new CapturedOutput();

        // Act
        var exitCode = ServiceCommandLine.Run(["--version"], output.Pipeline);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal($"{expected}{Environment.NewLine}", output.Out);
    }

    [Theory]
    [InlineData("serve")]
    [InlineData("wake")]
    public void Run_ShouldFailNamingTheSettings_WhenTheServiceCommandFindsNoHostSettings(string command)
    {
        // Arrange
        using var directory = TestFactory.Directory();
        _environment.Set("REMOTEBUILDTOOL_CONFIG", directory.Combine("config.json"));
        var output = new CapturedOutput();

        // Act
        var exitCode = ServiceCommandLine.Run([command], output.Pipeline);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains($"No service settings at {directory.Combine("config.json")}", output.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, ServeCommand.DefaultSettingsPath)]
    [InlineData("", ServeCommand.DefaultSettingsPath)]
    [InlineData("/srv/config.json", "/srv/config.json")]
    public void SettingsPath_ShouldFallBackToTheDefault_WhenNoSettingsFileIsNamed(string? named, string expected)
    {
        // Arrange
        string? Read(string name) => name == "REMOTEBUILDTOOL_CONFIG" ? named : null;

        // Act
        var path = ServiceCommandLine.SettingsPath(Read);

        // Assert
        Assert.Equal(expected, path);
    }
}
