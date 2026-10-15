using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class BuildSummarizerTests
{
    private const int Precision = 6;

    private static readonly DateTimeOffset Created = new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Write_ShouldSumTheStagesAndJobsIntoTheBuild_WhenBothAreThere()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.TelemetryOptions(directory);
        var root = options.TelemetryRoot!;
        WriteStage(root, Stage("plan", "host-1", Created.AddSeconds(10), durationMS: 20_000, cpuMS: 15_000, allocatedMS: 20_000, peak: 100, memory: 0.1));
        WriteStage(root, Stage("build-Debug", "host-2", Created.AddSeconds(40), durationMS: 30_000, cpuMS: 27_000, allocatedMS: 30_000, peak: 300, memory: 0.3) with
        {
            CacheHits = 8,
            CacheMisses = 14,
            DropVersion = "2026.10.14.001",
        });
        WriteStage(root, Stage("test-Debug", "host-1", Created.AddSeconds(80), durationMS: 10_000, cpuMS: 6_000, allocatedMS: 10_000, peak: 200, memory: 0.2) with
        {
            Succeeded = false,
            ExitCode = 1,
            FirstError = "Lib.Tests (exit 2) has failing tests.",
        });
        TelemetryWriter.Write(root, TelemetryWriter.TargetTable, "100-1.test-Debug.host-1", Created,
            [
                new TargetInfo { Project = "a", Toolchain = "dotnet", Kind = "test", CacheOutcome = "miss", TestsFailed = 2 },
                new TargetInfo { Project = "b", Toolchain = "dotnet", Kind = "build", CacheOutcome = "miss", TestsFailed = 9 },
            ],
            TelemetryJsonContext.Default.TargetInfo);
        TelemetryWriter.Write(root, TelemetryWriter.MachineTable, "100-1.plan.host-1", Created,
            [
                new MachineSample { BuildId = "100-1", Queue = "PR", Pool = "docker", Machine = "host-1", Stage = "plan", Time = Created, HostCpuUtilization = 0.2, HostMemoryUtilization = 0.5 },
                new MachineSample { BuildId = "100-1", Queue = "PR", Pool = "docker", Machine = "host-1", Stage = "plan", Time = Created, HostCpuUtilization = 0.6, HostMemoryUtilization = 0.7 },
            ],
            TelemetryJsonContext.Default.MachineSample);
        var jobs = TestFactory.WriteFile(directory.Combine("jobs.json"), """
            {
              "jobs": [
                { "name": "Plan", "created_at": "2026-10-14T10:00:00Z", "started_at": "2026-10-14T10:00:04Z", "completed_at": "2026-10-14T10:00:34Z" },
                { "name": "Debug build", "created_at": "2026-10-14T10:00:34Z", "started_at": "2026-10-14T10:00:38Z", "completed_at": "2026-10-14T10:01:18Z" },
                { "name": "Debug test", "created_at": "2026-10-14T10:01:18Z", "started_at": "2026-10-14T10:01:19Z", "completed_at": "2026-10-14T10:01:39Z" },
                { "name": "PR", "created_at": "2026-10-14T10:01:39Z", "started_at": "2026-10-14T10:01:41Z" }
              ]
            }
            """);
        var now = Created.AddSeconds(105);

        // Act
        BuildSummarizer.Write(root, options.Identity!, "failure", now, BuildTimeline.FromJobs(WorkflowJob.Parse(File.ReadAllText(jobs)), now), "service");

        // Assert
        var build = Assert.Single(TelemetryWriter.Read(root, TelemetryWriter.BuildTable, "100-1.", TelemetryJsonContext.Default.BuildInfo));
        Assert.Equal("100-1", build.BuildId);
        Assert.Equal("PR", build.Queue);
        Assert.Equal("docker", build.Pool);
        Assert.Equal("owner/repo", build.Repository);
        Assert.Equal(7, build.PullRequest);
        Assert.Equal("failure", build.Result);
        Assert.Equal(Created, build.CreatedTime);
        Assert.Equal(Created.AddSeconds(4), build.StartTime);
        Assert.Equal(now, build.EndTime);
        Assert.Equal(4_000 + 4_000 + 1_000 + 2_000, build.QueueTimeMS);
        Assert.Equal(30_000 + 40_000 + 20_000 + 4_000 - (20_000 + 30_000 + 10_000), build.PreparationTimeMS);
        Assert.Equal(105_000, build.E2ETimeMS);
        Assert.Equal(4, build.Jobs);
        Assert.Equal(3, build.Stages);
        Assert.Equal(60_000, build.StageTimeMS);
        Assert.Equal(48_000, build.CpuTimeMS);
        Assert.Equal(60_000, build.AllocatedCpuMS);
        Assert.Equal(48_000.0 / 60_000, build.CpuUtilization!.Value, Precision);
        Assert.Equal(300, build.PeakMemoryBytes);
        Assert.Equal(0.3, build.MemoryUtilization!.Value, Precision);
        Assert.Equal((0.2 + 0.6) / 2, build.HostCpuUtilization!.Value, Precision);
        Assert.Equal((0.5 + 0.7) / 2, build.HostMemoryUtilization!.Value, Precision);
        Assert.Equal(2, build.MachinesUsed);
        Assert.Equal(8, build.CacheHits);
        Assert.Equal(14, build.CacheMisses);
        Assert.Equal(2, build.TestsFailed);
        Assert.Equal("Lib.Tests (exit 2) has failing tests.", build.FirstError);
        Assert.Equal("PR_7/100-1", build.LogsPath);
        Assert.Equal("2026.10.14.001", build.DropVersion);
    }

    [Fact]
    public void Write_ShouldTakeTheTimesFromTheStagesAndLeaveTheJobsOut_WhenThereIsNoJobList()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.TelemetryOptions(directory);
        WriteStage(options.TelemetryRoot!, Stage("promote", "host-1", Created, durationMS: 1_000, cpuMS: 500, allocatedMS: 1_000, peak: 10, memory: 0.01));

        // Act
        BuildSummarizer.Write(options.TelemetryRoot!, options.Identity!, "success", Created.AddSeconds(5), BuildTimeline.Unknown, "service");

        // Assert
        var build = Assert.Single(TelemetryWriter.Read(options.TelemetryRoot!, TelemetryWriter.BuildTable, "100-1.", TelemetryJsonContext.Default.BuildInfo));
        Assert.Equal(Created, build.CreatedTime);
        Assert.Equal(Created, build.StartTime);
        Assert.Equal(5_000, build.E2ETimeMS);
        Assert.Null(build.QueueTimeMS);
        Assert.Null(build.PreparationTimeMS);
        Assert.Equal(0, build.Jobs);
        Assert.Null(build.CacheHits);
        Assert.Null(build.TestsFailed);
        Assert.Null(build.FirstError);
    }

    [Fact]
    public void Write_ShouldWriteABuildWithoutStages_WhenNoStageReachedTheShare()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.TelemetryOptions(directory);

        // Act
        BuildSummarizer.Write(options.TelemetryRoot!, options.Identity!, "cancelled", DateTimeOffset.UtcNow, BuildTimeline.Unknown, "service");

        // Assert
        var build = Assert.Single(TelemetryWriter.Read(options.TelemetryRoot!, TelemetryWriter.BuildTable, "100-1.", TelemetryJsonContext.Default.BuildInfo));
        Assert.Equal(0, build.Stages);
        Assert.Null(build.CpuTimeMS);
        Assert.Null(build.CpuUtilization);
        Assert.Null(build.E2ETimeMS);
        Assert.Null(build.HostCpuUtilization);
    }

    [Fact]
    public void Write_ShouldTakeTheLogsPathFromTheStages_WhenTheIdentityHasNone()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.TelemetryOptions(directory);
        WriteStage(options.TelemetryRoot!, Stage("plan", "host-1", Created, durationMS: 1_000, cpuMS: 500, allocatedMS: 1_000, peak: 10, memory: 0.01) with { LogsPath = "PR_7/100-1" });

        // Act
        BuildSummarizer.Write(options.TelemetryRoot!, options.Identity! with { LogsPath = null }, "success", Created.AddSeconds(5), BuildTimeline.Unknown, "service");

        // Assert
        var build = Assert.Single(TelemetryWriter.Read(options.TelemetryRoot!, TelemetryWriter.BuildTable, "100-1.", TelemetryJsonContext.Default.BuildInfo));
        Assert.Equal("PR_7/100-1", build.LogsPath);
    }

    private static StageInfo Stage(string name, string machine, DateTimeOffset start, long durationMS, long cpuMS, long allocatedMS, long peak, double memory) => new()
    {
        BuildId = "100-1",
        Queue = "PR",
        Pool = "docker",
        Machine = machine,
        Stage = name,
        StartTime = start,
        EndTime = start.AddMilliseconds(durationMS),
        DurationMS = durationMS,
        Succeeded = true,
        CpuTimeMS = cpuMS,
        AllocatedCpuMS = allocatedMS,
        PeakMemoryBytes = peak,
        MemoryUtilization = memory,
    };

    private static void WriteStage(string root, StageInfo stage) =>
        TelemetryWriter.Write(root, TelemetryWriter.StageTable, $"100-1.{stage.Stage}.{stage.Machine}", stage.StartTime, [stage], TelemetryJsonContext.Default.StageInfo);
}
