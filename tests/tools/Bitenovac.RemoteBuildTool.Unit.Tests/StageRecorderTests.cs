using Bitenovac.RemoteBuildTool.Core.Processes;
using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class StageRecorderTests
{
    private const int Precision = 6;

    private static readonly TimeSpan NoSampling = Timeout.InfiniteTimeSpan;

    [Fact]
    public void Complete_ShouldWriteTheStageWithItsContainersResources_WhenTheContainerIsMeasured()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.TelemetryOptions(directory);
        var output = new PipelineOutput(TextWriter.Null, TextWriter.Null);
        var counters = new Queue<MachineCounters>(
        [
            TestFactory.Counters(hostBusy: 100, hostTotal: 1000),
            TestFactory.Counters(hostBusy: 600, hostTotal: 2000, containerCpuSeconds: 3, containerPeak: 1L << 30, containerLimit: 2L << 30, cpuLimit: 2),
        ]);
        using var stage = StageRecorder.Start(options, output, "build", "Debug", NoSampling, counters.Dequeue);
        stage.SetProjects(projects: 5, cacheHits: 3);
        output.WriteFailure("build failed (exit 1).");

        // Act
        var exitCode = stage.Complete(1);

        // Assert
        Assert.Equal(1, exitCode);
        var info = Assert.Single(Read(directory, TelemetryWriter.StageTable, TelemetryJsonContext.Default.StageInfo));
        Assert.Equal("100-1", info.BuildId);
        Assert.Equal("PR", info.Queue);
        Assert.Equal("docker", info.Pool);
        Assert.Equal("host-1", info.Machine);
        Assert.Equal("build-Debug", info.Stage);
        Assert.Equal("Debug", info.Configuration);
        Assert.Equal(7, info.PullRequest);
        Assert.False(info.Succeeded);
        Assert.Equal("build failed (exit 1).", info.FirstError);
        Assert.Equal(5, info.Projects);
        Assert.Equal(3, info.CacheHits);
        Assert.Equal(2, info.CacheMisses);
        Assert.Equal(3000, info.CpuTimeMS);
        Assert.Equal(2, info.AllocatedCpus!.Value, Precision);
        Assert.Equal(2 * info.DurationMS, info.AllocatedCpuMS);
        Assert.Equal(1L << 30, info.PeakMemoryBytes);
        Assert.Equal(2L << 30, info.MemoryLimitBytes);
        Assert.Equal(0.5, info.MemoryUtilization!.Value, Precision);
        Assert.Equal("PR_7/100-1", info.LogsPath);
    }

    [Fact]
    public void Complete_ShouldReportTheTimeOfEachMeasuredPhaseOnly_WhenPhasesWereMeasured()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        using var stage = StageRecorder.Start(TestFactory.TelemetryOptions(directory), TestFactory.Silence(), "plan", null, NoSampling, () => TestFactory.Counters());
        stage.Add(StagePhase.Restore, TimeSpan.FromMilliseconds(1200));
        stage.Add(StagePhase.Restore, TimeSpan.FromMilliseconds(300));
        using (stage.Measure(StagePhase.Hashing))
        {
        }

        // Act
        stage.Complete(0);

        // Assert
        var info = Assert.Single(Read(directory, TelemetryWriter.StageTable, TelemetryJsonContext.Default.StageInfo));
        Assert.Equal("plan", info.Stage);
        Assert.True(info.Succeeded);
        Assert.Null(info.FirstError);
        Assert.Equal(1500, info.RestoreMS);
        Assert.NotNull(info.HashingMS);
        Assert.Null(info.EvaluationMS);
        Assert.Null(info.BuildMS);
        Assert.Null(info.Projects);
    }

    [Fact]
    public void Complete_ShouldFallBackToItsOwnProcesses_WhenTheContainerIsNotMeasured()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var unmeasured = new MachineCounters(null, null, 8L << 30, 6L << 30, null, null, null, null, null);
        using var stage = StageRecorder.Start(TestFactory.TelemetryOptions(directory), TestFactory.Silence(), "test", "Release", NoSampling, () => unmeasured);
        stage.AddProcess(new ResourceUsage(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(3), 6L << 30));

        // Act
        stage.Complete(0);

        // Assert
        var info = Assert.Single(Read(directory, TelemetryWriter.StageTable, TelemetryJsonContext.Default.StageInfo));
        Assert.True(info.CpuTimeMS >= 3000);
        Assert.Equal(Environment.ProcessorCount, info.AllocatedCpus!.Value, Precision);
        Assert.True(info.PeakMemoryBytes >= 6L << 30);
        Assert.Equal(8L << 30, info.MemoryLimitBytes);
    }

    [Fact]
    public void AddTarget_ShouldStampTheBuildAndStage_WhenTheStageCompletes()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        using var stage = StageRecorder.Start(TestFactory.TelemetryOptions(directory), TestFactory.Silence(), "build", "Debug", NoSampling, () => TestFactory.Counters());
        stage.AddTarget(new TargetInfo { Project = "src/A/A.csproj", Toolchain = "dotnet", Kind = "build", CacheOutcome = "hit", DurationMS = 12 });

        // Act
        stage.Complete(0);

        // Assert
        var target = Assert.Single(Read(directory, TelemetryWriter.TargetTable, TelemetryJsonContext.Default.TargetInfo));
        Assert.Equal(
            new TargetInfo
            {
                BuildId = "100-1",
                Queue = "PR",
                Pool = "docker",
                Machine = "host-1",
                Stage = "build-Debug",
                Configuration = "Debug",
                Project = "src/A/A.csproj",
                Toolchain = "dotnet",
                Kind = "build",
                CacheOutcome = "hit",
                DurationMS = 12,
                Sha = "abc123",
                PullRequest = 7,
                LogsPath = "PR_7/100-1",
            },
            target);
    }

    [Fact]
    public void AddTests_ShouldStampTheBuildAndStage_WhenTheStageCompletes()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        using var stage = StageRecorder.Start(TestFactory.TelemetryOptions(directory), TestFactory.Silence(), "test", "Debug", NoSampling, () => TestFactory.Counters());
        stage.AddTests([new TestInfo { Project = "tests/A/A.csproj", TestName = "A.Works", Outcome = "Passed", DurationMS = 3 }]);

        // Act
        stage.Complete(0);

        // Assert
        var test = Assert.Single(Read(directory, TelemetryWriter.TestTable, TelemetryJsonContext.Default.TestInfo));
        Assert.Equal("100-1", test.BuildId);
        Assert.Equal("test-Debug", test.Stage);
        Assert.Equal("host-1", test.Machine);
        Assert.Equal("A.Works", test.TestName);
        Assert.Equal(7, test.PullRequest);
    }

    [Fact]
    public void Complete_ShouldSampleTheMachineAtTheStartAndEnd_WhenTheStageIsShorterThanTheInterval()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var counters = new Queue<MachineCounters>(
        [
            TestFactory.Counters(hostBusy: 100, hostTotal: 1000),
            TestFactory.Counters(hostBusy: 350, hostTotal: 2000, containerCpuSeconds: 1.5),
        ]);
        using var stage = StageRecorder.Start(TestFactory.TelemetryOptions(directory), TestFactory.Silence(), "plan", null, NoSampling, counters.Dequeue);

        // Act
        stage.Complete(0);

        // Assert
        var samples = Read(directory, TelemetryWriter.MachineTable, TelemetryJsonContext.Default.MachineSample).OrderBy(sample => sample.Time).ToList();
        Assert.Equal(2, samples.Count);
        Assert.Null(samples[0].HostCpuUtilization);
        Assert.Equal((350.0 - 100) / (2000 - 1000), samples[1].HostCpuUtilization!.Value, Precision);
        Assert.Equal(1500, samples[1].ContainerCpuMS);
        Assert.Equal(0.25, samples[1].HostMemoryUtilization!.Value, Precision);
        Assert.All(samples, sample => Assert.Equal("plan", sample.Stage));
    }

    [Fact]
    public void Start_ShouldSampleTheMachineEveryInterval_WhileTheStageRuns()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var reads = 0;
        using var stage = StageRecorder.Start(TestFactory.TelemetryOptions(directory), TestFactory.Silence(), "plan", null, TimeSpan.FromMilliseconds(10), () =>
        {
            Interlocked.Increment(ref reads);
            return TestFactory.Counters();
        });

        // Act
        SpinWait.SpinUntil(() => Volatile.Read(ref reads) >= 4, TimeSpan.FromSeconds(10));
        stage.Complete(0);

        // Assert
        Assert.True(Read(directory, TelemetryWriter.MachineTable, TelemetryJsonContext.Default.MachineSample).Count() >= 4);
    }

    [Fact]
    public void Dispose_ShouldRecordAFailedStage_WhenItNeverCompleted()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var stage = StageRecorder.Start(TestFactory.TelemetryOptions(directory), TestFactory.Silence(), "test", "Debug", NoSampling, () => TestFactory.Counters());

        // Act
        stage.Dispose();
        stage.Dispose();

        // Assert
        var info = Assert.Single(Read(directory, TelemetryWriter.StageTable, TelemetryJsonContext.Default.StageInfo));
        Assert.Equal(1, info.ExitCode);
    }

    [Fact]
    public void Complete_ShouldRecordTheDropVersion_WhenTheStageDroppedARelease()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        using var stage = StageRecorder.Start(TestFactory.TelemetryOptions(directory), TestFactory.Silence(), "release", null, NoSampling, () => TestFactory.Counters());
        stage.SetDropVersion("2026.10.14.001");

        // Act
        stage.Complete(0);

        // Assert
        var info = Assert.Single(Read(directory, TelemetryWriter.StageTable, TelemetryJsonContext.Default.StageInfo));
        Assert.Equal("2026.10.14.001", info.DropVersion);
    }

    [Fact]
    public void Complete_ShouldWriteNothing_WhenThereIsNoTelemetryShare()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        using var stage = StageRecorder.Start(options, TestFactory.Silence(), "plan", null, NoSampling, () => TestFactory.Counters());

        // Act
        var exitCode = stage.Complete(0);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Empty(Directory.EnumerateFiles(directory.Path, "*.jsonl", SearchOption.AllDirectories));
        Assert.Equal("plan", stage.Name);
    }

    private static IEnumerable<T> Read<T>(TemporaryDirectory directory, string table, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo) =>
        TelemetryWriter.Read(directory.Combine("telemetry"), table, "100-1.", typeInfo);
}
