using Bitenovac.RemoteBuildTool.Commands;
using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Integration.Tests;

public sealed class TelemetryIntegrationTests
{
    private readonly CapturedOutput _output = new();

    [Fact]
    public void Pipeline_ShouldRecordEveryStageProjectAndTestCase_WhenThereIsATelemetryShare()
    {
        // Arrange
        using var fixture = FixtureRepository.Create();
        var telemetry = fixture.Combine("artifacts/telemetry");
        var identity = new BuildIdentity("100-1", "PR", "docker", "host-1");
        fixture.With(telemetryRoot: telemetry, identity: identity);

        // Act
        var planExitCode = PlanCommand.Run(fixture.Options, _output.Pipeline);
        var buildExitCode = BuildCommand.Run(fixture.Options, "Debug", _output.Pipeline);
        var testExitCode = TestCommand.Run(fixture.Options, "Debug", _output.Pipeline);
        var reportExitCode = ReportBuildCommand.Run(fixture.Options, _output.Pipeline, "success", null);

        // Assert
        Assert.Equal([0, 0, 0, 0], [planExitCode, buildExitCode, testExitCode, reportExitCode]);
        var prefix = identity.BuildId + ".";

        var stages = TelemetryWriter.Read(telemetry, TelemetryWriter.StageTable, prefix, TelemetryJsonContext.Default.StageInfo)
            .ToDictionary(stage => stage.Stage);
        Assert.Equal(["build-Debug", "plan", "test-Debug"], stages.Keys.Order(StringComparer.Ordinal));
        Assert.All(stages.Values, stage => Assert.True(stage.Succeeded));
        Assert.NotNull(stages["plan"].EvaluationMS);
        Assert.NotNull(stages["plan"].RestoreMS);
        Assert.Equal(4, stages["plan"].Projects);
        Assert.Equal(0, stages["plan"].CacheHits);
        Assert.NotNull(stages["build-Debug"].BuildMS);
        Assert.NotNull(stages["test-Debug"].TestMS);
        Assert.NotNull(stages["test-Debug"].CoverageMS);

        var targets = TelemetryWriter.Read(telemetry, TelemetryWriter.TargetTable, prefix, TelemetryJsonContext.Default.TargetInfo).ToList();
        var built = targets.Where(target => target.Kind == "build").ToList();
        Assert.Equal(
            [FixtureRepository.LibraryProject, FixtureRepository.TestProject],
            built.Select(target => target.Project).Order(StringComparer.Ordinal));
        Assert.All(built, target =>
        {
            Assert.Equal("miss", target.CacheOutcome);
            Assert.True(target.DurationMS > 0);
            Assert.Null(target.CpuTimeMS);
        });

        var tested = Assert.Single(targets, target => target.Kind == "test");
        Assert.Equal(FixtureRepository.TestProject, tested.Project);
        Assert.Equal("Other", tested.TestKind);
        Assert.True(tested.TestsTotal > 0);
        Assert.Equal(tested.TestsTotal, tested.TestsPassed);
        Assert.Equal(0, tested.TestsFailed);

        var tests = TelemetryWriter.Read(telemetry, TelemetryWriter.TestTable, prefix, TelemetryJsonContext.Default.TestInfo).ToList();
        Assert.Equal(tested.TestsTotal, tests.Count);
        Assert.All(tests, test => Assert.Equal("Passed", test.Outcome));

        Assert.NotEmpty(TelemetryWriter.Read(telemetry, TelemetryWriter.MachineTable, prefix, TelemetryJsonContext.Default.MachineSample));
        Assert.Single(TelemetryWriter.Read(telemetry, TelemetryWriter.BuildTable, prefix, TelemetryJsonContext.Default.BuildInfo));
    }
}
