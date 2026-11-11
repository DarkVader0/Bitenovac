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

    private static PlanEntry[] Entries() =>
    [
        TestFactory.Entry("src/App/App.csproj", references: ["src/Lib/Lib.csproj"]),
        TestFactory.Entry("src/Lib/Lib.csproj"),
        TestFactory.Entry("src/Other/Other.csproj"),
    ];
}
