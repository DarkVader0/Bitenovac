using System.Diagnostics;
using System.Globalization;
using Bitenovac.RemoteBuildTool.Core.Coverage;
using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Core.Planning;
using Bitenovac.RemoteBuildTool.Planning;
using Bitenovac.RemoteBuildTool.Reporting;
using Bitenovac.RemoteBuildTool.Storage;
using Bitenovac.RemoteBuildTool.Telemetry;
using Bitenovac.RemoteBuildTool.Testing;
using Bitenovac.RemoteBuildTool.Toolchains;

namespace Bitenovac.RemoteBuildTool.Commands;

/// <summary>
/// Provides the <c>test</c> pipeline command, which runs or reuses the result of every selected
/// test project and then, for Debug, gates coverage.
/// </summary>
/// <remarks>
/// A test project whose plan decision is a hit and whose <c>&lt;CacheTestResults&gt;</c> policy
/// allows it reuses its cached Cobertura report from <c>main</c> instead of running again.
/// </remarks>
internal static class TestCommand
{
    /// <summary>
    /// The path prefixes, within a stored entry, of a cached test result.
    /// </summary>
    private static readonly string[] TestResultPrefixes = ["tests/"];

    public static int Run(PipelineOptions options, string configuration, PipelineOutput output)
    {
        using var stage = StageRecorder.Start(options, output, "test", configuration);
        var plan = PlanState.Load(options.PlanFile);
        var entries = plan.For(configuration);
        if (entries.Count == 0)
        {
            output.WriteLine("Nothing selected; nothing to test.");
            return stage.Complete(0);
        }

        using var toolchains = ToolchainRegistry.Create(options.RepositoryRoot);
        var prStore = new LocalVolumeArtifactStore(options.PrStoreRoot);
        var mainStore = new LocalVolumeArtifactStore(options.MainStoreRoot);

        bool materialised;
        using (stage.Measure(StagePhase.CacheRestore))
            materialised = MaterialiseBuildOutputs(output, options, entries, configuration, prStore, mainStore, toolchains);
        if (!materialised)
            return stage.Complete(1);

        var testEntries = entries.Where(entry => entry.IsTestProject).ToList();
        if (testEntries.Count == 0)
        {
            output.WriteLine("No selected test projects; nothing to run.");
            return stage.Complete(0);
        }

        var coverageDirectory = options.CoverageDirectory(configuration);
        if (Directory.Exists(coverageDirectory))
            Directory.Delete(coverageDirectory, recursive: true);
        Directory.CreateDirectory(coverageDirectory);

        var failed = new List<string>();
        var noTestsRan = new List<string>();
        var toRun = new List<(PlanEntry Entry, string Name)>();
        var summary = new TestSummary(configuration);

        using (output.Section($"Testing {testEntries.Count} project(s) ({configuration})"))
        {
            foreach (var entry in testEntries)
            {
                var name = Path.GetFileNameWithoutExtension(entry.ProjectPath);

                var started = Stopwatch.GetTimestamp();
                bool reused;
                using (stage.Measure(StagePhase.CacheRestore))
                    reused = entry.Hit && entry.CacheTestResults && ReuseCachedResult(new ProjectId(entry.ProjectPath), configuration, name, mainStore, coverageDirectory);

                if (reused)
                {
                    output.WriteLine($"--- {name} (reused)");
                    summary.Reused(name);
                    stage.AddTarget(TestTarget(entry, name) with
                    {
                        CacheOutcome = "reused",
                        DurationMS = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                        Succeeded = true,
                    });
                    continue;
                }

                toRun.Add((entry, name));
            }

            var jobs = toRun
                .Select(run => new TestJob(run.Name, () => toolchains.For(run.Entry.Toolchain).RunTests(run.Entry, configuration, run.Name, coverageDirectory)))
                .ToList();
            IReadOnlyList<TestRunResult> results;
            using (stage.Measure(StagePhase.Test))
            {
                results = TestSuite.RunAll(jobs, coverageDirectory, PipelineOptions.MaxParallelism(), (job, result) =>
                {
                    Report(output, job, result);
                    summary.Ran(job.Name, result);
                });
            }

            for (var index = 0; index < toRun.Count; index++)
            {
                var (entry, name) = toRun[index];
                var result = results[index];
                Record(stage, entry, name, result);

                switch (result.Outcome)
                {
                    case TestRunOutcome.Passed:
                        if (entry.CacheTestResults)
                        {
                            using (stage.Measure(StagePhase.Store))
                                StageTestResult(new ProjectId(entry.ProjectPath), configuration, name, coverageDirectory, entry, prStore);
                        }
                        break;
                    case TestRunOutcome.NoTestsRan:
                        noTestsRan.Add(name);
                        break;
                    case TestRunOutcome.Failed:
                        failed.Add($"{name} (exit {result.ExitCode})");
                        break;
                }
            }
        }

        stage.SetProjects(testEntries.Count, testEntries.Count - toRun.Count);
        output.AppendSummary(summary.ToMarkdown());

        if (noTestsRan.Count > 0)
            output.WriteLine($"warning: test projects that ran no tests: {string.Join(", ", noTestsRan)}");

        if (failed.Count > 0)
        {
            foreach (var project in failed)
                output.WriteFailure($"{project} has failing tests.");
            return stage.Complete(1);
        }

        output.WriteLine($"All selected tests passed ({configuration}).");

        if (configuration != "Debug")
            return stage.Complete(0);

        int gateExitCode;
        using (stage.Measure(StagePhase.Coverage))
            gateExitCode = RunCoverageGate(entries, coverageDirectory, options, output);
        if (gateExitCode != 0)
            output.WriteFailure("a gated project is below its coverage policy; the lines above name it.");

        output.AppendSummary(gateExitCode == 0 ? "Coverage gate passed.\n" : "**Coverage gate failed.**\n");
        return stage.Complete(gateExitCode);
    }

    private static TargetInfo TestTarget(PlanEntry entry, string name) => new()
    {
        Project = entry.ProjectPath,
        Toolchain = entry.Toolchain,
        Kind = "test",
        TestKind = TestKind.Of(name),
        CacheOutcome = "miss",
        WhyCacheMiss = entry.Hit ? null : entry.WhyCacheMiss,
        OwnHash = entry.OwnHash,
        FullHash = entry.FullHash,
    };

    // A test project that ran is a miss even when its build was a hit: its results were not reused.
    private static void Record(StageRecorder stage, PlanEntry entry, string name, TestRunResult result)
    {
        var cases = result.ReportPath is { } report ? TrxReader.Read(report) : [];
        var reported = result.ReportPath is not null;

        stage.AddTests(cases.Select(test => new TestInfo
        {
            Project = entry.ProjectPath,
            TestName = test.Name,
            Outcome = test.Outcome,
            DurationMS = test.Duration is { } duration ? (long)duration.TotalMilliseconds : null,
            ErrorMessage = test.ErrorMessage,
        }));

        if (result.Usage is { } usage)
            stage.AddProcess(usage);

        stage.AddTarget(TestTarget(entry, name) with
        {
            DurationMS = result.Usage is { } elapsed ? (long)elapsed.Elapsed.TotalMilliseconds : null,
            CpuTimeMS = result.Usage?.CpuTime is { } cpu ? (long)cpu.TotalMilliseconds : null,
            PeakMemoryBytes = result.Usage?.PeakMemoryBytes,
            Succeeded = result.Outcome != TestRunOutcome.Failed,
            ExitCode = result.ExitCode,
            TestsTotal = reported ? cases.Count : null,
            TestsPassed = reported ? cases.Count(test => test.Outcome == "Passed") : null,
            TestsFailed = reported ? cases.Count(test => test.Outcome is "Failed" or "Error" or "Timeout" or "Aborted") : null,
            TestsSkipped = reported ? cases.Count(test => test.Outcome == "NotExecuted") : null,
        });
    }

    // A project that passed is folded away; one that failed stays open, as it is what the reader
    // came for.
    private static void Report(PipelineOutput output, TestJob job, TestRunResult result)
    {
        var took = result.Usage is { } usage
            ? string.Create(CultureInfo.InvariantCulture, $" in {usage.Elapsed.TotalSeconds:0.0} s")
            : "";
        var body = result.Usage is { } measured
            ? $"{result.Output}resources: test {job.Name} {measured}{Environment.NewLine}"
            : result.Output;

        switch (result.Outcome)
        {
            case TestRunOutcome.Failed:
                output.WriteLine($"--- {job.Name}: failed (exit {result.ExitCode}){took}");
                output.Out.Write(body);
                break;
            case TestRunOutcome.NoTestsRan:
                output.WriteGroup($"{job.Name}: ran no tests{took}", body);
                break;
            default:
                output.WriteGroup($"{job.Name}: passed{took}", body);
                break;
        }
    }

    private static bool MaterialiseBuildOutputs(
        PipelineOutput output,
        PipelineOptions options,
        IReadOnlyList<PlanEntry> entries,
        string configuration,
        LocalVolumeArtifactStore prStore,
        LocalVolumeArtifactStore mainStore,
        ToolchainRegistry toolchains)
    {
        var materialised = 0;
        var skipped = 0;

        foreach (var entry in entries)
        {
            var project = new ProjectId(entry.ProjectPath);
            var projectDirectory = Path.GetDirectoryName(entry.FullPath)!;
            var outputDirectories = toolchains.For(entry.Toolchain).OutputDirectories;

            if (MaterialisedMarker.Matches(options.RepositoryRoot, project, configuration, entry.FullHash, Path.Combine(projectDirectory, outputDirectories[0])))
            {
                skipped++;
                continue;
            }

            var source = entry.Hit ? mainStore : prStore;

            if (!source.TryGet(project, configuration, projectDirectory, out _, [.. outputDirectories.Select(directory => directory + "/")]))
            {
                output.WriteFailure($"no build output available for {entry.ProjectPath}. Run 'build {configuration}' first.");
                return false;
            }

            MaterialisedMarker.Write(options.RepositoryRoot, project, configuration, entry.FullHash);
            materialised++;
        }

        if (materialised > 0 || skipped > 0)
            output.WriteLine($"{configuration}: materialised {materialised} project(s)"
                + (skipped > 0 ? $", {skipped} already present" : "") + ".");

        return true;
    }

    private static bool ReuseCachedResult(ProjectId project, string configuration, string name, LocalVolumeArtifactStore mainStore, string coverageDirectory)
    {
        var staging = Directory.CreateTempSubdirectory("bitenovac-remotebuildtool-reuse-").FullName;
        try
        {
            if (!mainStore.TryGet(project, configuration, staging, out _, TestResultPrefixes))
                return false;

            var testsDirectory = Path.Combine(staging, "tests");
            if (!Directory.Exists(testsDirectory))
                return false;

            foreach (var file in Directory.EnumerateFiles(testsDirectory))
                File.Copy(file, Path.Combine(coverageDirectory, Path.GetFileName(file)), overwrite: true);

            return true;
        }
        finally
        {
            Directory.Delete(staging, recursive: true);
        }
    }

    private static void StageTestResult(ProjectId project, string configuration, string name, string coverageDirectory, PlanEntry entry, LocalVolumeArtifactStore prStore)
    {
        var staging = Directory.CreateTempSubdirectory("bitenovac-remotebuildtool-stage-").FullName;
        try
        {
            var testsDirectory = Path.Combine(staging, "tests");
            Directory.CreateDirectory(testsDirectory);
            // The TRX report belongs to this run's telemetry, not to the result a later run reuses.
            foreach (var file in Directory.EnumerateFiles(coverageDirectory, $"{name}*").Where(file => !file.EndsWith(".trx", StringComparison.OrdinalIgnoreCase)))
                File.Copy(file, Path.Combine(testsDirectory, Path.GetFileName(file)), overwrite: true);

            prStore.AddFiles(project, configuration, new StoredTargetHash(entry.OwnHash, entry.FullHash), staging);
        }
        finally
        {
            Directory.Delete(staging, recursive: true);
        }
    }

    private static int RunCoverageGate(IReadOnlyList<PlanEntry> entries, string coverageDirectory, PipelineOptions options, PipelineOutput output)
    {
        var gated = entries
            .Where(entry => entry.ShouldGateCoverage && !entry.ExcludeFromCoverage)
            .ToDictionary(
                entry => new ProjectId(entry.ProjectPath),
                entry => new CoveragePolicy(entry.MinimumLineCoverage, entry.MinimumBranchCoverage));
        var coverageNames = entries.ToDictionary(entry => new ProjectId(entry.ProjectPath), entry => entry.CoverageName);
        var location = new CoverageReportLocation(options.RepositoryRoot, coverageDirectory, options.CoverageReportDirectory("Debug"), options.CoverageHtml);

        return CoverageCheck.Run(gated, coverageNames, location, output.Out, output.Error);
    }
}
