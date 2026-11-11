using Bitenovac.RemoteBuildTool.Commands;
using Bitenovac.RemoteBuildTool.Planning;
using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class ConcurrentBuildTests
{
    [Fact]
    public void Run_ShouldBuildEveryUnitInACallOfItsOwn_WhenThereAreSeveralSlots()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        var entries = Entries();
        var schedule = BuildSharding.Schedule(entries, new SlotLimits(2, 2), cores: 2);
        using var toolchain = new FakeToolchain();
        var output = new CapturedOutput();
        using var stage = StageRecorder.Start(options, output.Pipeline, "build", "Debug");

        // Act
        var exitCode = ConcurrentBuild.Run(toolchain, options, entries, schedule, "Debug", output.Pipeline, stage);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(
            [["src/Lib/Lib.csproj", "src/App/App.csproj"], ["src/Other/Other.csproj"]],
            toolchain.Builds.OrderByDescending(build => build.Count));
        Assert.Contains("--- unit 1: built 2 project(s) in ", output.Out, StringComparison.Ordinal);
        Assert.Contains("--- unit 2: built 1 project(s) in ", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldBuildTheOtherUnitsAndReturnTheFailure_WhenAUnitFails()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        var entries = Entries();
        var schedule = BuildSharding.Schedule(entries, SlotLimits.One, cores: 1);
        using var toolchain = new FakeToolchain
        {
            BuildResult = unit => unit.Any(entry => entry.ProjectPath == "src/Lib/Lib.csproj") ? 3 : 0,
        };
        var output = new CapturedOutput();
        using var stage = StageRecorder.Start(options, output.Pipeline, "build", "Debug");

        // Act
        var exitCode = ConcurrentBuild.Run(toolchain, options, entries, schedule, "Debug", output.Pipeline, stage);

        // Assert
        // The unit with the longer chain, Lib and App, goes first and fails; Other is still built.
        Assert.Equal(3, exitCode);
        Assert.Contains("src/Other/Other.csproj", toolchain.Built);
        Assert.Contains("--- unit 1: failed 2 project(s) in ", output.Out, StringComparison.Ordinal);
        Assert.Contains("--- unit 2: built 1 project(s) in ", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldBuildAUnitOnlyAfterTheUnitItDependsOn_WhenAGroupWasSplit()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        var entries = SharedLibrary();
        var schedule = BuildSharding.Schedule(entries, new SlotLimits(2, 2), cores: 2);
        using var toolchain = new FakeToolchain();
        var output = new CapturedOutput();
        using var stage = StageRecorder.Start(options, output.Pipeline, "build", "Debug");

        // Act
        var exitCode = ConcurrentBuild.Run(toolchain, options, entries, schedule, "Debug", output.Pipeline, stage);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(["src/Lib/Lib.csproj"], toolchain.Builds[0]);
        Assert.Equal(
            [["src/App1/App1.csproj"], ["src/App2/App2.csproj"]],
            toolchain.Builds.Skip(1).OrderBy(build => build[0], StringComparer.Ordinal));
    }

    [Fact]
    public void Run_ShouldSkipTheUnitsBehindIt_WhenTheUnitTheyDependOnFails()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        var entries = SharedLibrary();
        var schedule = BuildSharding.Schedule(entries, new SlotLimits(2, 2), cores: 2);
        using var toolchain = new FakeToolchain { BuildResult = _ => 5 };
        var output = new CapturedOutput();
        using var stage = StageRecorder.Start(options, output.Pipeline, "build", "Debug");

        // Act
        var exitCode = ConcurrentBuild.Run(toolchain, options, entries, schedule, "Debug", output.Pipeline, stage);

        // Assert
        Assert.Equal(5, exitCode);
        Assert.Equal(["src/Lib/Lib.csproj"], toolchain.Built);
        Assert.Contains("--- unit 1: failed 1 project(s) in ", output.Out, StringComparison.Ordinal);
        Assert.Contains("--- unit 2: skipped 1 project(s), as a unit it depends on did not build", output.Out, StringComparison.Ordinal);
        Assert.Contains("--- unit 3: skipped 1 project(s), as a unit it depends on did not build", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldRecordEachUnitWithWhatWasExpectedOfIt_WhenTheStageKeepsTelemetry()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.TelemetryOptions(directory);
        var entries = Entries();
        var schedule = BuildSharding.Schedule(entries, SlotLimits.One, cores: 1);
        using var toolchain = new FakeToolchain
        {
            BuildResult = unit => unit.Any(entry => entry.ProjectPath == "src/Other/Other.csproj") ? 4 : 0,
        };
        var stage = StageRecorder.Start(options, TestFactory.Silence(), "build", "Debug");

        // Act
        var exitCode = ConcurrentBuild.Run(toolchain, options, entries, schedule, "Debug", TestFactory.Silence(), stage);
        stage.Complete(exitCode);

        // Assert
        // With no build time known a project is expected to take a second: Lib and App are a chain
        // of two, so 2 s of work and a 2 s critical path; Other is 1 s of each.
        var units = TelemetryWriter.Read(options.TelemetryRoot!, TelemetryTables.Unit, "100-1.", TelemetryJsonContext.Default.UnitInfo)
            .OrderBy(unit => unit.Unit)
            .Select(unit => (unit.Unit, unit.Slot, unit.Slots, unit.Projects, unit.FirstProject, unit.ExpectedWorkMS, unit.ExpectedCriticalPathMS, unit.Succeeded, unit.ExitCode));
        Assert.Equal(
            [(1, 1, 1, 2, "src/Lib/Lib.csproj", 2000L, 2000L, true, 0), (2, 1, 1, 1, "src/Other/Other.csproj", 1000L, 1000L, false, 4)],
            units);
    }

    private static PlanEntry[] SharedLibrary() =>
    [
        TestFactory.Entry("src/App1/App1.csproj", references: ["src/Lib/Lib.csproj"]),
        TestFactory.Entry("src/App2/App2.csproj", references: ["src/Lib/Lib.csproj"]),
        TestFactory.Entry("src/Lib/Lib.csproj"),
    ];

    private static PlanEntry[] Entries() =>
    [
        TestFactory.Entry("src/App/App.csproj", references: ["src/Lib/Lib.csproj"]),
        TestFactory.Entry("src/Lib/Lib.csproj"),
        TestFactory.Entry("src/Other/Other.csproj"),
    ];
}
