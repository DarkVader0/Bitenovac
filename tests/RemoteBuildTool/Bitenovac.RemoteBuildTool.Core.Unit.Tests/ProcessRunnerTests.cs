using System.ComponentModel;
using Bitenovac.RemoteBuildTool.Core.Processes;

namespace Bitenovac.RemoteBuildTool.Core.Unit.Tests;

public sealed class ProcessRunnerTests
{
    [Fact]
    public void Run_ShouldReportZero_WhenTheProcessSucceeds()
    {
        // Arrange
        using var directory = TestFactory.Directory();

        // Act
        var exitCode = ProcessRunner.Run("dotnet", ["--version"], directory.Path).ExitCode;

        // Assert
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Run_ShouldReportTheProcessExitCode_WhenItFails()
    {
        // Arrange
        using var directory = TestFactory.Directory();

        // Act
        var exitCode = ProcessRunner.Run("dotnet", ["exec", directory.Combine("not-an-assembly.dll")], directory.Path).ExitCode;

        // Assert
        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void Run_ShouldStartTheProcess_WhenAnEnvironmentIsSupplied()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var environment = new Dictionary<string, string> { ["REMOTEBUILDTOOL_TEST_VARIABLE"] = "set" };

        // Act
        var exitCode = ProcessRunner.Run("dotnet", ["--version"], directory.Path, environment).ExitCode;

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Null(Environment.GetEnvironmentVariable("REMOTEBUILDTOOL_TEST_VARIABLE"));
    }

    [Fact]
    public void RunCaptured_ShouldReturnTheOutputInsteadOfPrintingIt_WhenTheProcessSucceeds()
    {
        // Arrange
        using var directory = TestFactory.Directory();

        // Act
        var (exitCode, output, _) = ProcessRunner.RunCaptured("dotnet", ["--version"], directory.Path);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Matches(@"^\d+\.\d+\.\d+", output);
    }

    [Fact]
    public void RunCaptured_ShouldCaptureTheErrorStream_WhenTheProcessFails()
    {
        // Arrange
        using var directory = TestFactory.Directory();

        // Act
        var (exitCode, output, _) = ProcessRunner.RunCaptured("dotnet", ["exec", directory.Combine("not-an-assembly.dll")], directory.Path);

        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.Contains("not-an-assembly.dll", output, StringComparison.Ordinal);
    }

    [Fact]
    public void RunCaptured_ShouldThrow_WhenTheExecutableDoesNotExist()
    {
        // Arrange
        using var directory = TestFactory.Directory();

        // Act
        Action act = () => ProcessRunner.RunCaptured("bitenovac-no-such-executable", [], directory.Path);

        // Assert
        Assert.Throws<Win32Exception>(act);
    }

    [Fact]
    public void Run_ShouldReportHowLongTheProcessRan_WhenItExits()
    {
        // Arrange
        using var directory = TestFactory.Directory();

        // Act
        var result = ProcessRunner.Run("dotnet", ["--version"], directory.Path);

        // Assert
        Assert.True(result.Usage.Elapsed > TimeSpan.Zero);
        Assert.Empty(result.Output);
    }

    [Fact]
    public void RunCaptured_ShouldMeasureTheProcessTree_WhenTheSystemHasProc()
    {
        // Arrange
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Only Linux has /proc.");
        using var directory = TestFactory.Directory();

        // Act
        var usage = ProcessRunner.RunCaptured("dotnet", ["--version"], directory.Path).Usage;

        // Assert
        Assert.NotNull(usage.CpuTime);
        Assert.True(usage.PeakMemoryBytes > 0);
    }

    [Fact]
    public void Run_ShouldThrow_WhenTheExecutableDoesNotExist()
    {
        // Arrange
        using var directory = TestFactory.Directory();

        // Act
        Action act = () => ProcessRunner.Run("bitenovac-no-such-executable", [], directory.Path);

        // Assert
        Assert.Throws<Win32Exception>(act);
    }
}
