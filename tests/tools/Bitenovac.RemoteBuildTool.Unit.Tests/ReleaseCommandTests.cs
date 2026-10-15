using Bitenovac.RemoteBuildTool.Commands;
using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class ReleaseCommandTests
{
    private static readonly DateOnly Today = new(2026, 10, 14);

    [Fact]
    public void Run_ShouldThrow_WhenNoPlanWasWritten()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));

        // Act
        Action act = () => ReleaseCommand.Run(options, TestFactory.Silence());

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Run_ShouldFailWithoutCreatingADrop_WhenThePlanSelectedNothingForRelease()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        TestFactory.Plan("Debug", TestFactory.Entry("src/A/A.csproj")).Save(options.PlanFile);

        // Act
        var exitCode = ReleaseCommand.Run(options, TestFactory.Silence());

        // Assert
        Assert.Equal(1, exitCode);
        Assert.False(Directory.Exists(options.DropRoot));
    }

    [Fact]
    public void Run_ShouldLeaveTestProjectsOut_WhenThePlanSelectedThemForRelease()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        TestFactory.Plan("Release", TestFactory.Entry("src/A/A.csproj"), TestFactory.Entry("tests/A.Tests/A.Tests.csproj", isTestProject: true)).Save(options.PlanFile);
        var toolchain = new FakeToolchain();
        using var registry = toolchain.Registry();

        // Act
        var exitCode = ReleaseCommand.Run(options, TestFactory.Silence(), Today, registry);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(["src/A/A.csproj"], toolchain.Published);
    }

    [Fact]
    public void Run_ShouldFailWithoutCreatingADrop_WhenThePlanSelectedOnlyTestProjects()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        TestFactory.Plan("Release", TestFactory.Entry("tests/A.Tests/A.Tests.csproj", isTestProject: true)).Save(options.PlanFile);
        var toolchain = new FakeToolchain();
        using var registry = toolchain.Registry();
        var output = new CapturedOutput();

        // Act
        var exitCode = ReleaseCommand.Run(options, output.Pipeline, Today, registry);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Empty(toolchain.Published);
        Assert.False(Directory.Exists(options.DropRoot));
        Assert.Contains("nothing to deploy for Release", output.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldDropWhatWasPublished_WhenAFailedReleaseLeftItsStagingBehind()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        TestFactory.Plan("Release", TestFactory.Entry("src/A/A.csproj")).Save(options.PlanFile);
        TestFactory.WriteFile(options.DropRoot, ".staging-2026.10.14.001/left-over.txt", "");
        using var registry = new FakeToolchain().Registry();

        // Act
        var exitCode = ReleaseCommand.Run(options, TestFactory.Silence(), Today, registry);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(["2026.10.14.001"], Directory.GetFileSystemEntries(options.DropRoot).Select(Path.GetFileName));
        Assert.Equal("2026.10.14.001", File.ReadAllText(Path.Combine(options.DropRoot, "2026.10.14.001", "published.txt")));
        Assert.False(File.Exists(Path.Combine(options.DropRoot, "2026.10.14.001", "left-over.txt")));
    }

    [Fact]
    public void Run_ShouldRecordTheDropVersion_WhenTheReleaseIsDropped()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.TelemetryOptions(directory);
        TestFactory.Plan("Release", TestFactory.Entry("src/A/A.csproj")).Save(options.PlanFile);
        using var registry = new FakeToolchain().Registry();

        // Act
        var exitCode = ReleaseCommand.Run(options, TestFactory.Silence(), Today, registry);

        // Assert
        Assert.Equal(0, exitCode);
        var stage = Assert.Single(TelemetryWriter.Read(options.TelemetryRoot!, TelemetryWriter.StageTable, "100-1.", TelemetryJsonContext.Default.StageInfo));
        Assert.Equal("2026.10.14.001", stage.DropVersion);
    }

    [Fact]
    public void Run_ShouldDropNothing_WhenAToolchainFailsToPublish()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        TestFactory.Plan("Release", TestFactory.Entry("src/A/A.csproj")).Save(options.PlanFile);
        using var registry = new FakeToolchain { PublishExitCode = 3 }.Registry();
        var output = new CapturedOutput();

        // Act
        var exitCode = ReleaseCommand.Run(options, output.Pipeline, Today, registry);

        // Assert
        Assert.Equal(3, exitCode);
        Assert.Empty(Directory.GetFileSystemEntries(options.DropRoot));
        Assert.Contains("release 2026.10.14.001 failed; nothing was dropped.", output.Error, StringComparison.Ordinal);
    }
}
