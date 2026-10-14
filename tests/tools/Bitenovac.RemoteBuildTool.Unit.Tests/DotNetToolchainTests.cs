using Bitenovac.RemoteBuildTool.Core.Processes;
using Bitenovac.RemoteBuildTool.MsBuild;
using Bitenovac.RemoteBuildTool.Planning;
using Bitenovac.RemoteBuildTool.Telemetry;
using Bitenovac.RemoteBuildTool.Testing;
using Bitenovac.RemoteBuildTool.Toolchains;
using Bitenovac.RemoteBuildTool.Toolchains.DotNet;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class DotNetToolchainTests
{
    [Fact]
    public void Prepare_ShouldFail_WhenTheRestoreFails()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.TelemetryOptions(directory);
        var msBuild = new FakeMsBuild { RestoreExitCode = 1 };
        using var toolchain = new DotNetToolchain(directory.Path, msBuild);
        var output = new CapturedOutput();
        using var stage = StageRecorder.Start(options, output.Pipeline, "plan");

        // Act
        var preparation = toolchain.Prepare(options, ByConfiguration(Project(directory, "src/A/A.csproj")), output.Pipeline, stage);

        // Assert
        Assert.Equal(1, preparation.ExitCode);
        Assert.Contains("restore failed (exit 1).", output.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Prepare_ShouldReadThePackageClosureAgain_WhenTheRestoreSucceeds()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.TelemetryOptions(directory);
        using var toolchain = new DotNetToolchain(directory.Path, new FakeMsBuild());
        using var stage = StageRecorder.Start(options, TestFactory.Silence(), "plan");
        var project = Project(directory, "src/A/A.csproj") with { OwnHashInputs = ["source:A.cs=1", "package:Old/1.0.0"] };

        // Act
        var preparation = toolchain.Prepare(options, ByConfiguration(project), TestFactory.Silence(), stage);

        // Assert
        Assert.Equal(0, preparation.ExitCode);
        Assert.Equal(["source:A.cs=1"], preparation.ByConfiguration["Debug"].Single().OwnHashInputs);
    }

    [Fact]
    public void Build_ShouldNotBuild_WhenRestoringWhatThePlanDidNotRestoreFails()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.TelemetryOptions(directory);
        var msBuild = new FakeMsBuild { RestoreExitCode = 2 };
        using var toolchain = new DotNetToolchain(directory.Path, msBuild);
        var output = new CapturedOutput();
        using var stage = StageRecorder.Start(options, output.Pipeline, "build", "Debug");

        // Act
        var exitCode = toolchain.Build(options, [Entry(directory, "src/A/A.csproj")], "Debug", output.Pipeline, stage);

        // Assert
        Assert.Equal(2, exitCode);
        Assert.Equal(["restore"], msBuild.Calls);
        Assert.Contains("restore failed (exit 2).", output.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ShouldRecordEveryProjectAsFailed_WhenTheBuildFails()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.TelemetryOptions(directory);
        var msBuild = new FakeMsBuild { BuildExitCode = 1 };
        using var toolchain = new DotNetToolchain(directory.Path, msBuild);
        var output = new CapturedOutput();
        var stage = StageRecorder.Start(options, output.Pipeline, "build", "Debug");

        // Act
        var exitCode = toolchain.Build(options, [Entry(directory, "src/A/A.csproj")], "Debug", output.Pipeline, stage);
        stage.Complete(exitCode);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Equal(["restore", "build"], msBuild.Calls);
        Assert.Contains("build failed (exit 1).", output.Error, StringComparison.Ordinal);
        var target = Assert.Single(Targets(options));
        Assert.Equal((false, 1, null), (target.Succeeded, target.ExitCode, target.DurationMS));
    }

    [Fact]
    public void Build_ShouldTakeEachProjectsTimeFromTheLog_WhenThePlanAlreadyRestoredIt()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.TelemetryOptions(directory);
        var entry = Entry(directory, "src/A/A.csproj");
        TestFactory.WriteFile(directory.Combine("src/A/obj/project.assets.json"), "{}");
        var msBuild = new FakeMsBuild
        {
            PerformanceLog = $"""
                Project Performance Summary:
                      346 ms  {entry.FullPath}   1 calls
                """,
        };
        using var toolchain = new DotNetToolchain(directory.Path, msBuild);
        var stage = StageRecorder.Start(options, TestFactory.Silence(), "build", "Debug");

        // Act
        var exitCode = toolchain.Build(options, [entry], "Debug", TestFactory.Silence(), stage);
        stage.Complete(exitCode);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(["build"], msBuild.Calls);
        var target = Assert.Single(Targets(options));
        Assert.Equal((true, 346L, "main's entry could not be materialised"), (target.Succeeded, target.DurationMS, target.WhyCacheMiss));
    }

    [Fact]
    public void Publish_ShouldDeployNothing_WhenTheReleaseBuildFails()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.TelemetryOptions(directory);
        var msBuild = new FakeMsBuild { BuildExitCode = 1 };
        using var toolchain = new DotNetToolchain(directory.Path, msBuild);
        using var stage = StageRecorder.Start(options, TestFactory.Silence(), "release");

        // Act
        var exitCode = toolchain.Publish(options, [Entry(directory, "src/A/A.csproj", Executable)], "2026.10.14.001", directory.Combine("drop"), TestFactory.Silence(), stage);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.DoesNotContain("publish", msBuild.Calls);
    }

    [Fact]
    public void Publish_ShouldPublishApplicationsAndPackLibraries_WhenTheReleaseBuildSucceeds()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.TelemetryOptions(directory);
        var msBuild = new FakeMsBuild { PublishCpuTime = TimeSpan.FromSeconds(3) };
        using var toolchain = new DotNetToolchain(directory.Path, msBuild);
        var output = new CapturedOutput();
        var stage = StageRecorder.Start(options, output.Pipeline, "release");
        PlanEntry[] entries =
        [
            Entry(directory, "src/App/App.csproj", Executable),
            Entry(directory, "src/Tool/Tool.csproj", new() { ["OutputType"] = "Exe", ["PackAsTool"] = "true", ["IsPackable"] = "true" }),
            Entry(directory, "src/Lib/Lib.csproj", new() { ["IsPackable"] = "true" }),
            Entry(directory, "src/Internal/Internal.csproj", new() { ["IsPackable"] = "false" }),
            Entry(directory, "src/Sample/Sample.csproj", new() { ["OutputType"] = "WinExe", ["IsPublishable"] = "false" }),
            Entry(directory, "tests/App.Tests/App.Tests.csproj", Executable, isTestProject: true),
        ];

        // Act
        var exitCode = toolchain.Publish(options, entries, "2026.10.14.001", directory.Combine("drop"), output.Pipeline, stage);
        stage.Complete(exitCode);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(["restore", "build", "publish App", "pack Tool", "pack Lib"], msBuild.Calls);
        Assert.Contains("--- Internal: nothing to deploy", output.Out, StringComparison.Ordinal);
        Assert.Contains("--- Sample: nothing to deploy", output.Out, StringComparison.Ordinal);
        var deployed = Targets(options).Where(target => target.Kind != "build").OrderBy(target => target.Project, StringComparer.Ordinal);
        Assert.Equal(
            [("src/App/App.csproj", "publish", 3000L), ("src/Lib/Lib.csproj", "pack", null), ("src/Tool/Tool.csproj", "pack", null)],
            deployed.Select(target => (target.Project, target.Kind!, target.CpuTimeMS)));
    }

    [Fact]
    public void Publish_ShouldStop_WhenAProjectFailsToDeploy()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.TelemetryOptions(directory);
        var msBuild = new FakeMsBuild { PublishExitCode = 4 };
        using var toolchain = new DotNetToolchain(directory.Path, msBuild);
        var output = new CapturedOutput();
        using var stage = StageRecorder.Start(options, output.Pipeline, "release");
        PlanEntry[] entries = [Entry(directory, "src/App/App.csproj", Executable), Entry(directory, "src/Other/Other.csproj", Executable)];

        // Act
        var exitCode = toolchain.Publish(options, entries, "2026.10.14.001", directory.Combine("drop"), output.Pipeline, stage);

        // Assert
        Assert.Equal(4, exitCode);
        Assert.Equal(["restore", "build", "publish App"], msBuild.Calls);
        Assert.Contains("could not deploy src/App/App.csproj (exit 4).", output.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void RunTests_ShouldThrow_WhenTheTestExecutableWasNeverBuilt()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        using var toolchain = new DotNetToolchain(directory.Path, new FakeMsBuild());
        var entry = Entry(directory, "tests/A/A.Tests.csproj", new() { ["RunCommand"] = directory.Combine("tests/A/bin/A.Tests") });

        // Act
        Action act = () => toolchain.RunTests(entry, "Debug", "A.Tests", directory.Combine("results"));

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.StartsWith("No built test executable found for tests/A/A.Tests.csproj (Debug)", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RunTests_ShouldRunTheBuiltExecutable_WhenItExists()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        using var toolchain = new DotNetToolchain(directory.Path, new FakeMsBuild());
        var entry = Entry(directory, "tests/A/A.Tests.csproj", new() { ["RunCommand"] = NoTestsExecutable(directory) });

        // Act
        var result = toolchain.RunTests(entry, "Debug", "A.Tests", directory.Combine("results"));

        // Assert
        Assert.Equal(TestRunOutcome.NoTestsRan, result.Outcome);
        Assert.NotNull(result.Usage);
    }

    [Theory]
    [InlineData("package:Microsoft.Testing.Extensions.TrxReport/2.0.0", nameof(TestReport.Trx))]
    [InlineData("package:xunit.v3.core/3.1.0", nameof(TestReport.XunitTrx))]
    [InlineData("package:xunit.v3.core.mtp-v1/3.1.0", nameof(TestReport.XunitTrx))]
    [InlineData("package:XUNIT.V3.CORE.MTP-V2/3.1.0", nameof(TestReport.XunitTrx))]
    [InlineData("package:NUnit/4.0.0", nameof(TestReport.None))]
    public void ReportOf_ShouldFollowTheTestFramework_WhenThePackageClosureNamesIt(string package, string expected)
    {
        // Arrange
        var entry = TestFactory.Entry("tests/A/A.Tests.csproj", ownHashInputs: ["source:A.cs=1", package]);

        // Act
        var report = DotNetToolchain.ReportOf(entry);

        // Assert
        Assert.Equal(Enum.Parse<TestReport>(expected), report);
    }

    /// <summary>
    /// Writes a script that exits as a test project that ran no tests does, runnable as it stands.
    /// </summary>
    private static string NoTestsExecutable(TemporaryDirectory directory)
    {
        if (OperatingSystem.IsWindows())
            return TestFactory.WriteFile(directory.Combine("tests/A/bin/A.Tests.cmd"), "@exit 8\r\n");

        var script = TestFactory.WriteFile(directory.Combine("tests/A/bin/A.Tests"), "#!/bin/sh\nexit 8\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return script;
    }

    private static Dictionary<string, string> Executable => new() { ["OutputType"] = "Exe" };

    private static EvaluatedProject Project(TemporaryDirectory directory, string relativePath) =>
        new(
            TestFactory.Id(relativePath),
            directory.Combine(relativePath),
            DotNetToolchain.ToolchainName,
            [],
            IsTestProject: false,
            ExcludeFromCoverage: false,
            MinimumLineCoverage: 100,
            MinimumBranchCoverage: 100,
            CacheTestResults: true,
            Path.GetFileNameWithoutExtension(relativePath),
            new Dictionary<string, string>(),
            []);

    private static IReadOnlyDictionary<string, IReadOnlyList<EvaluatedProject>> ByConfiguration(EvaluatedProject project) =>
        new Dictionary<string, IReadOnlyList<EvaluatedProject>> { ["Debug"] = [project], ["Release"] = [project] };

    private static PlanEntry Entry(TemporaryDirectory directory, string relativePath, Dictionary<string, string>? properties = null, bool isTestProject = false) =>
        TestFactory.Entry(relativePath, fullPath: directory.Combine(relativePath), properties: properties, isTestProject: isTestProject);

    private static IEnumerable<TargetInfo> Targets(PipelineOptions options) =>
        TelemetryWriter.Read(options.TelemetryRoot!, TelemetryWriter.TargetTable, "100-1.", TelemetryJsonContext.Default.TargetInfo);

    private sealed class FakeMsBuild : IMsBuildRunner
    {
        public int RestoreExitCode { get; init; }

        public int BuildExitCode { get; init; }

        public int PublishExitCode { get; init; }

        public TimeSpan? PublishCpuTime { get; init; }

        public string? PerformanceLog { get; init; }

        public List<string> Calls { get; } = [];

        public ProcessResult Restore(string repositoryRoot, IEnumerable<string> projectFullPaths, string configuration, string solutionPath, MsBuildLogs logs) =>
            Result("restore", RestoreExitCode);

        public ProcessResult Build(
            string repositoryRoot,
            IEnumerable<string> projectFullPaths,
            string configuration,
            string solutionPath,
            IReadOnlyDictionary<string, string> defaultProperties,
            MsBuildLogs logs)
        {
            if (PerformanceLog is not null)
                TestFactory.WriteFile(logs.PerformanceLogPath, PerformanceLog);

            return Result("build", BuildExitCode);
        }

        public ProcessResult Pack(string repositoryRoot, string projectFullPath, IReadOnlyDictionary<string, string> defaultProperties, string outputDirectory, MsBuildLogs logs) =>
            Result($"pack {Path.GetFileNameWithoutExtension(projectFullPath)}", 0);

        public ProcessResult Publish(string repositoryRoot, string projectFullPath, IReadOnlyDictionary<string, string> defaultProperties, string outputDirectory, MsBuildLogs logs) =>
            Result($"publish {Path.GetFileNameWithoutExtension(projectFullPath)}", PublishExitCode, PublishCpuTime);

        private ProcessResult Result(string call, int exitCode, TimeSpan? cpuTime = null)
        {
            Calls.Add(call);
            return new ProcessResult(exitCode, "", new ResourceUsage(TimeSpan.FromSeconds(1), cpuTime, null));
        }
    }
}
