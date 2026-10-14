using Bitenovac.RemoteBuildTool.Commands;
using Bitenovac.RemoteBuildTool.Core.Planning;
using Bitenovac.RemoteBuildTool.Core.Processes;
using Bitenovac.RemoteBuildTool.Planning;
using Bitenovac.RemoteBuildTool.Storage;
using Bitenovac.RemoteBuildTool.Telemetry;
using Bitenovac.RemoteBuildTool.Testing;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class TestCommandTests
{
    [Fact]
    public void Run_ShouldThrow_WhenNoPlanWasWritten()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));

        // Act
        Action act = () => TestCommand.Run(options, "Debug", TestFactory.Silence());

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Run_ShouldSucceed_WhenTheConfigurationSelectedNothing()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        TestFactory.Plan("Debug", TestFactory.Entry("src/A/A.csproj")).Save(options.PlanFile);

        // Act
        var exitCode = TestCommand.Run(options, "Release", TestFactory.Silence());

        // Assert
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Run_ShouldFail_WhenNoStoreHoldsTheSelectedProjectsOutput()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        var entry = TestFactory.Entry("src/A/A.csproj", fullPath: directory.Combine("src/A/A.csproj"));
        TestFactory.Plan("Debug", entry).Save(options.PlanFile);

        // Act
        var exitCode = TestCommand.Run(options, "Debug", TestFactory.Silence());

        // Assert
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Run_ShouldMaterialiseAndStop_WhenNoSelectedProjectIsATestProject()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        var project = TestFactory.Id("src/A/A.csproj");
        var staged = directory.Combine("staged");
        TestFactory.WriteFile(staged, "bin/Debug/A.dll", "assembly");
        new LocalVolumeArtifactStore(options.PrStoreRoot)
            .Put(project, "Debug", staged, new StoredTargetHash("own", "full"));
        var entry = TestFactory.Entry(
            project.Value,
            fullPath: directory.Combine("src/A/A.csproj"),
            ownHash: "own",
            fullHash: "full");
        TestFactory.Plan("Debug", entry).Save(options.PlanFile);

        // Act
        var exitCode = TestCommand.Run(options, "Debug", TestFactory.Silence());

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal("assembly", File.ReadAllText(directory.Combine("src/A/bin/Debug/A.dll")));
        Assert.True(MaterialisedMarker.Matches(
            options.RepositoryRoot, project, "Debug", "full", directory.Combine("src/A")));
    }

    [Fact]
    public void Run_ShouldFailTheProject_WhenItsTestExecutableWasNeverBuilt()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        var project = TestFactory.Id("tests/A/A.Tests.csproj");
        var staged = directory.Combine("staged");
        TestFactory.WriteFile(staged, "bin/Debug/A.Tests.dll", "assembly");
        new LocalVolumeArtifactStore(options.PrStoreRoot)
            .Put(project, "Debug", staged, new StoredTargetHash("own", "full"));
        var entry = TestFactory.Entry(
            project.Value,
            fullPath: directory.Combine("tests/A/A.Tests.csproj"),
            properties: new Dictionary<string, string> { ["RunCommand"] = directory.Combine("tests/A/bin/Release/A.Tests") },
            isTestProject: true,
            ownHash: "own",
            fullHash: "full");
        TestFactory.Plan("Debug", entry).Save(options.PlanFile);
        var output = new CapturedOutput();

        // Act
        var exitCode = TestCommand.Run(options, "Debug", output.Pipeline);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("No built test executable found for tests/A/A.Tests.csproj", output.Out, StringComparison.Ordinal);
        Assert.Contains("A.Tests (exit -1)", output.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldTakeAHitFromMainRatherThanTheRunStore_WhenTheEntryWasNeverStaged()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        var project = TestFactory.Id("src/A/A.csproj");
        var staged = directory.Combine("staged");
        TestFactory.WriteFile(staged, "bin/Debug/A.dll", "from main");
        new LocalVolumeArtifactStore(options.MainStoreRoot)
            .Put(project, "Debug", staged, new StoredTargetHash("own", "full"));
        var entry = TestFactory.Entry(
            project.Value,
            fullPath: directory.Combine("src/A/A.csproj"),
            ownHash: "own",
            fullHash: "full",
            hit: true);
        TestFactory.Plan("Debug", entry).Save(options.PlanFile);

        // Act
        var exitCode = TestCommand.Run(options, "Debug", TestFactory.Silence());

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal("from main", File.ReadAllText(directory.Combine("src/A/bin/Debug/A.dll")));
    }

    [Fact]
    public void Run_ShouldSkipMaterialising_WhenTheWorkspaceAlreadyHoldsThisHash()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        var project = TestFactory.Id("src/A/A.csproj");

        TestFactory.WriteFile(directory.Combine("src/A"), "bin/Debug/A.dll", "left by build");
        MaterialisedMarker.Write(options.RepositoryRoot, project, "Debug", "full");
        var entry = TestFactory.Entry(
            project.Value,
            fullPath: directory.Combine("src/A/A.csproj"),
            ownHash: "own",
            fullHash: "full");
        TestFactory.Plan("Debug", entry).Save(options.PlanFile);

        // Act
        var exitCode = TestCommand.Run(options, "Debug", TestFactory.Silence());

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal("left by build", File.ReadAllText(directory.Combine("src/A/bin/Debug/A.dll")));
    }

    [Fact]
    public void Run_ShouldRecordEachTestCase_WhenTheProjectWritesAReport()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.TelemetryOptions(directory);
        TestFactory.WriteFile(options.CoverageDirectory("Debug"), "stale.cobertura.xml", "<coverage/>");
        Stage(options, options.PrStoreRoot, hit: false);
        var report = TestFactory.WriteFile(directory.Combine("A.Tests.trx"), """
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <Results>
                <UnitTestResult testName="A.Passes" outcome="Passed" duration="00:00:00.25" />
                <UnitTestResult testName="A.Skips" outcome="NotExecuted" />
              </Results>
            </TestRun>
            """);
        var toolchain = new FakeToolchain
        {
            Tests = (_, results) => new TestRunResult(
                TestRunOutcome.Passed, 0, results, "", new ResourceUsage(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(500), 1024), report),
        };
        using var registry = toolchain.Registry();
        var output = new CapturedOutput();

        // Act
        var exitCode = TestCommand.Run(options, "Debug", output.Pipeline, registry);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(Path.Combine(options.CoverageDirectory("Debug"), "stale.cobertura.xml")));
        Assert.Contains("A.Tests: passed in 2.0 s", output.Out, StringComparison.Ordinal);
        var target = Assert.Single(TelemetryWriter.Read(options.TelemetryRoot!, TelemetryWriter.TargetTable, "100-1.", TelemetryJsonContext.Default.TargetInfo));
        Assert.Equal((2, 1, 0, 1), (target.TestsTotal, target.TestsPassed, target.TestsFailed, target.TestsSkipped));
        Assert.Equal((2000L, 500L, 1024L), (target.DurationMS, target.CpuTimeMS, target.PeakMemoryBytes));
        var tests = TelemetryWriter.Read(options.TelemetryRoot!, TelemetryWriter.TestTable, "100-1.", TelemetryJsonContext.Default.TestInfo);
        Assert.Equal([("A.Passes", 250L), ("A.Skips", null)], tests.Select(test => (test.TestName, test.DurationMS)).Order());
    }

    [Fact]
    public void Run_ShouldLeaveTheCountsOut_WhenTheProjectWritesNoReport()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.TelemetryOptions(directory);
        Stage(options, options.PrStoreRoot, hit: false);
        var toolchain = new FakeToolchain
        {
            Tests = (_, results) => new TestRunResult(TestRunOutcome.NoTestsRan, 8, results, "", new ResourceUsage(TimeSpan.FromSeconds(1), null, null)),
        };
        using var registry = toolchain.Registry();
        var output = new CapturedOutput();

        // Act
        var exitCode = TestCommand.Run(options, "Debug", output.Pipeline, registry);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("A.Tests: ran no tests in 1.0 s", output.Out, StringComparison.Ordinal);
        Assert.Contains("warning: test projects that ran no tests: A.Tests", output.Out, StringComparison.Ordinal);
        var target = Assert.Single(TelemetryWriter.Read(options.TelemetryRoot!, TelemetryWriter.TargetTable, "100-1.", TelemetryJsonContext.Default.TargetInfo));
        Assert.Equal((null, null, null, null), (target.TestsTotal, target.TestsPassed, target.TestsFailed, target.TestsSkipped));
        Assert.Equal((1000L, null, null), (target.DurationMS, target.CpuTimeMS, target.PeakMemoryBytes));
    }

    [Fact]
    public void Run_ShouldReuseTheCachedResult_WhenMainHoldsOneForTheHit()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        Stage(options, options.MainStoreRoot, hit: true, cachedResult: true);
        var toolchain = new FakeToolchain { Tests = (_, _) => throw new InvalidOperationException("The project ran.") };
        using var registry = toolchain.Registry();
        var output = new CapturedOutput();

        // Act
        var exitCode = TestCommand.Run(options, "Debug", output.Pipeline, registry);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("--- A.Tests (reused)", output.Out, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(options.CoverageDirectory("Debug"), "A.Tests.coverage.cobertura.xml")));
    }

    [Fact]
    public void Run_ShouldRunTheProject_WhenMainsEntryHoldsNoCachedResult()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        Stage(options, options.MainStoreRoot, hit: true);
        var runs = 0;
        var toolchain = new FakeToolchain { Tests = (_, results) => new TestRunResult(TestRunOutcome.Passed, 0, results, $"run {++runs}") };
        using var registry = toolchain.Registry();

        // Act
        var exitCode = TestCommand.Run(options, "Debug", TestFactory.Silence(), registry);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(1, runs);
    }

    [Fact]
    public void Run_ShouldRunTheProject_WhenTheWorkspaceHoldsTheHitButMainNoLongerDoes()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        var entry = Stage(options, directory.Combine("elsewhere"), hit: true);
        TestFactory.WriteFile(directory.Combine("tests/A"), "bin/Debug/A.Tests.dll", "left by build");
        MaterialisedMarker.Write(options.RepositoryRoot, TestFactory.Id(entry.ProjectPath), "Debug", entry.FullHash);
        var runs = 0;
        var toolchain = new FakeToolchain { Tests = (_, results) => new TestRunResult(TestRunOutcome.Passed, 0, results, $"run {++runs}") };
        using var registry = toolchain.Registry();
        var output = new CapturedOutput();

        // Act
        var exitCode = TestCommand.Run(options, "Debug", output.Pipeline, registry);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(1, runs);
        Assert.Contains("Debug: materialised 0 project(s), 1 already present.", output.Out, StringComparison.Ordinal);
    }

    /// <summary>
    /// Stores a built test project in <paramref name="store"/>, with its coverage when
    /// <paramref name="cachedResult"/> is set, and plans it alone for Debug outside the coverage gate.
    /// </summary>
    private static PlanEntry Stage(PipelineOptions options, string store, bool hit, bool cachedResult = false)
    {
        var project = TestFactory.Id("tests/A/A.Tests.csproj");
        var staged = Path.Combine(options.RepositoryRoot, "staged");
        TestFactory.WriteFile(staged, "bin/Debug/A.Tests.dll", "assembly");
        if (cachedResult)
            TestFactory.WriteFile(staged, "tests/A.Tests.coverage.cobertura.xml", "<coverage/>");
        new LocalVolumeArtifactStore(store).Put(project, "Debug", staged, new StoredTargetHash("own", "full"));

        var entry = TestFactory.Entry(
            project.Value,
            fullPath: Path.Combine(options.RepositoryRoot, "tests", "A", "A.Tests.csproj"),
            isTestProject: true,
            excludeFromCoverage: true,
            ownHash: "own",
            fullHash: "full",
            hit: hit);
        TestFactory.Plan("Debug", entry).Save(options.PlanFile);
        return entry;
    }
}
