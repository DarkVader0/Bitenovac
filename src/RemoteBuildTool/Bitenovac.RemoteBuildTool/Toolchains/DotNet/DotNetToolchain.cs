using System.Runtime.CompilerServices;
using Bitenovac.RemoteBuildTool.Core.Processes;
using Bitenovac.RemoteBuildTool.Hashing;
using Bitenovac.RemoteBuildTool.MsBuild;
using Bitenovac.RemoteBuildTool.Planning;
using Bitenovac.RemoteBuildTool.Telemetry;
using Bitenovac.RemoteBuildTool.Testing;

namespace Bitenovac.RemoteBuildTool.Toolchains.DotNet;

/// <summary>
/// Represents the toolchain for MSBuild projects.
/// </summary>
/// <remarks>
/// <para>
/// Projects are evaluated in-process, restored and built through one synthetic solution, and
/// tested by running the executable of each Microsoft.Testing.Platform test project.
/// </para>
/// <para>
/// On release, an executable that is not a .NET tool and not <c>IsPublishable=false</c> is
/// published under <c>apps/</c>, and any other packable project is packed under <c>packages/</c>.
/// </para>
/// </remarks>
internal sealed class DotNetToolchain(string repositoryRoot, IMsBuildRunner msBuild) : IToolchain
{
    public const string ToolchainName = "dotnet";

    /// <summary>
    /// The name of the property that holds the test executable reported by MSBuild, which is the
    /// apphost of a Microsoft.Testing.Platform test project.
    /// </summary>
    public const string RunCommandProperty = "RunCommand";

    public const string TargetFrameworksProperty = "TargetFrameworks";

    public const string OutputTypeProperty = "OutputType";

    public const string IsPackableProperty = "IsPackable";

    public const string PackAsToolProperty = "PackAsTool";

    public const string IsPublishableProperty = "IsPublishable";

    private MsBuildProjectEvaluator? _evaluator;

    public string Name => ToolchainName;

    public IReadOnlyList<string> OutputDirectories { get; } = ["bin", "obj"];

    public IReadOnlyList<string> Discover() =>
        ProjectDiscovery.FindRelativePaths(repositoryRoot);

    // Created on first use: build and test never evaluate, so a run that has no MSBuild project
    // never needs MSBuild in-process. One collection must serve every evaluation, since loading
    // the same project twice into it throws.
    public IReadOnlyList<EvaluatedProject> Evaluate(IReadOnlyList<string> relativePaths, string configuration)
    {
        MsBuildRegistration.EnsureRegistered();
        return EvaluateRegistered(relativePaths, configuration);
    }

    public ToolchainPreparation Prepare(
        PipelineOptions options,
        IReadOnlyDictionary<string, IReadOnlyList<EvaluatedProject>> byConfiguration,
        PipelineOutput output,
        StageRecorder stage)
    {
        var debug = byConfiguration["Debug"];

        var restoreExitCode = Run("restore", $"Restoring {debug.Count} project(s)", StagePhase.Restore, options, output, stage, logs => msBuild.Restore(
            options.RepositoryRoot,
            debug.Select(project => project.FullPath),
            configuration: "Debug",
            options.SyntheticSolutionPath("restore"),
            logs)).ExitCode;

        if (restoreExitCode != 0)
        {
            output.WriteFailure($"restore failed (exit {restoreExitCode}).");
            return ToolchainPreparation.Failed(restoreExitCode);
        }

        var refreshed = byConfiguration.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<EvaluatedProject>)[.. pair.Value.Select(PackageClosureReader.Refresh)]);

        RestoreOutputs.Save(options.RepositoryRoot, refreshed["Debug"].Select(project => project.FullPath), options.RestoreOutputsRoot);

        return new ToolchainPreparation(0, refreshed);
    }

    public int Build(PipelineOptions options, IReadOnlyList<PlanEntry> entries, string configuration, PipelineOutput output, StageRecorder stage) =>
        Build(options, entries, configuration, new Dictionary<string, string>(), output, stage, slot: null);

    public int Build(PipelineOptions options, IReadOnlyList<PlanEntry> entries, string configuration, BuildSlot slot, PipelineOutput output, StageRecorder stage) =>
        Build(options, entries, configuration, new Dictionary<string, string>(), output, stage, slot);

    public int Publish(PipelineOptions options, IReadOnlyList<PlanEntry> entries, string version, string dropDirectory, PipelineOutput output, StageRecorder stage)
    {
        // VersionPrefix, not Version: SDK targets derive Version from VersionPrefix only while Version
        // is unset, so a release Version would mask a project's own VersionPrefix.
        var defaultProperties = new Dictionary<string, string> { ["VersionPrefix"] = version };

        var exitCode = Build(options, entries, "Release", defaultProperties, output, stage, slot: null);
        if (exitCode != 0)
            return exitCode;

        foreach (var entry in entries)
        {
            var name = Path.GetFileNameWithoutExtension(entry.ProjectPath);

            IReadOnlyList<DeploymentStep> steps = DeploymentOf(entry) switch
            {
                Deployment.Publish => PublishSteps(options, entry, name, defaultProperties, Path.Combine(dropDirectory, "apps", name)),
                Deployment.Pack => [new DeploymentStep("pack", $"pack-{name}", $"{name}: pack", StagePhase.Pack, logs =>
                    msBuild.Pack(options.RepositoryRoot, entry.FullPath, defaultProperties, Path.Combine(dropDirectory, "packages"), logs))],
                _ => [],
            };

            if (steps.Count == 0)
            {
                output.WriteLine($"--- {name}: nothing to deploy");
                continue;
            }

            foreach (var step in steps)
            {
                var result = Run(step.Name, step.Title, step.Phase, options, output, stage, step.Run);

                stage.AddTarget(new TargetInfo
                {
                    Project = entry.ProjectPath,
                    Toolchain = Name,
                    Kind = step.Kind,
                    CacheOutcome = "miss",
                    OwnHash = entry.OwnHash,
                    FullHash = entry.FullHash,
                    DurationMS = (long)result.Usage.Elapsed.TotalMilliseconds,
                    CpuTimeMS = result.Usage.CpuTime is { } cpu ? (long)cpu.TotalMilliseconds : null,
                    PeakMemoryBytes = result.Usage.PeakMemoryBytes,
                    Succeeded = result.ExitCode == 0,
                    ExitCode = result.ExitCode,
                });

                if (result.ExitCode != 0)
                {
                    output.WriteFailure($"could not deploy {entry.ProjectPath} (exit {result.ExitCode}).");
                    return result.ExitCode;
                }
            }
        }

        return 0;
    }

    // An app with one target framework is published into its folder; one with several is
    // published once for each, into a folder per framework inside it.
    private IReadOnlyList<DeploymentStep> PublishSteps(
        PipelineOptions options, PlanEntry entry, string name, IReadOnlyDictionary<string, string> defaultProperties, string appDirectory)
    {
        var targetFrameworks = TargetFrameworksOf(entry);
        if (targetFrameworks.Length == 0)
        {
            return [new DeploymentStep("publish", $"publish-{name}", $"{name}: publish", StagePhase.Publish, logs =>
                msBuild.Publish(options.RepositoryRoot, entry.FullPath, defaultProperties, appDirectory, logs, targetFramework: null))];
        }

        return [.. targetFrameworks.Select(targetFramework => new DeploymentStep(
            "publish", $"publish-{name}-{targetFramework}", $"{name}: publish {targetFramework}", StagePhase.Publish, logs =>
                msBuild.Publish(options.RepositoryRoot, entry.FullPath, defaultProperties, Path.Combine(appDirectory, targetFramework), logs, targetFramework)))];
    }

    private static string[] TargetFrameworksOf(PlanEntry entry) =>
        entry.Properties.GetValueOrDefault(TargetFrameworksProperty, "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private int Build(
        PipelineOptions options,
        IReadOnlyList<PlanEntry> entries,
        string configuration,
        IReadOnlyDictionary<string, string> defaultProperties,
        PipelineOutput output,
        StageRecorder stage,
        BuildSlot? slot)
    {
        // A unit built beside others keeps its solution and its logs under its own name.
        var name = slot is null ? configuration : $"{configuration}-{slot.Name}";

        var unrestored = entries
            .Where(entry => !RestoreOutputs.Materialise(options.RepositoryRoot, entry.FullPath, options.RestoreOutputsRoot))
            .ToList();

        if (unrestored.Count > 0)
        {
            var restoreExitCode = Run($"restore-{name}", $"Restoring {unrestored.Count} project(s) plan did not restore", StagePhase.Restore, options, output, stage, logs => msBuild.Restore(
                options.RepositoryRoot,
                unrestored.Select(entry => entry.FullPath),
                configuration,
                options.SyntheticSolutionPath($"restore-{name}"),
                logs)).ExitCode;

            if (restoreExitCode != 0)
            {
                output.WriteFailure($"restore failed (exit {restoreExitCode}).");
                return restoreExitCode;
            }
        }

        var step = $"build-{name}";
        var exitCode = Run(step, $"Building {entries.Count} project(s) ({configuration})", StagePhase.Build, options, output, stage, logs => msBuild.Build(
            options.RepositoryRoot,
            entries.Select(entry => entry.FullPath),
            configuration,
            options.SyntheticSolutionPath(step),
            defaultProperties,
            logs,
            slot)).ExitCode;

        // One MSBuild process builds them all, so a project's CPU time and memory are not its own.
        var durations = MsBuildPerformance.ProjectDurations(options.MsBuildLogs(step).PerformanceLogPath);
        foreach (var entry in entries)
        {
            stage.AddTarget(new TargetInfo
            {
                Project = entry.ProjectPath,
                Toolchain = Name,
                Kind = "build",
                CacheOutcome = "miss",
                WhyCacheMiss = entry.WhyCacheMiss ?? "main's entry could not be materialised",
                OwnHash = entry.OwnHash,
                FullHash = entry.FullHash,
                DurationMS = durations.TryGetValue(Path.GetFullPath(entry.FullPath), out var duration) ? (long)duration.TotalMilliseconds : null,
                Succeeded = exitCode == 0,
                ExitCode = exitCode,
            });
        }

        if (exitCode != 0)
            output.WriteFailure($"build failed (exit {exitCode}).");

        return exitCode;
    }

    public IReadOnlyList<TestVariant> TestVariants(PlanEntry entry)
    {
        var targetFrameworks = TargetFrameworksOf(entry);
        if (targetFrameworks.Length == 0)
            return [new TestVariant("", entry)];

        // Each target framework has a test executable of its own.
        return [.. targetFrameworks.Select(targetFramework => new TestVariant(targetFramework, entry with
        {
            Properties = new Dictionary<string, string>(entry.Properties)
            {
                [RunCommandProperty] = entry.Properties.GetValueOrDefault(RunCommandOf(targetFramework), ""),
            },
        }))];
    }

    /// <summary>
    /// Returns the plan property that holds the run command of one target framework of a project
    /// that has several.
    /// </summary>
    /// <param name="targetFramework">The target framework.</param>
    /// <returns>The property's name.</returns>
    public static string RunCommandOf(string targetFramework) => $"{RunCommandProperty}:{targetFramework}";

    public TestRunResult RunTests(PlanEntry entry, string configuration, string resultsFilePrefix, string resultsDirectory)
    {
        var executable = entry.Properties.GetValueOrDefault(RunCommandProperty, "");
        if (!File.Exists(executable))
            throw new InvalidOperationException($"No built test executable found for {entry.ProjectPath} ({configuration}) at '{executable}'. Run 'build' first.");

        return TestRunner.Run(executable, resultsFilePrefix, resultsDirectory, ReportOf(entry));
    }

    // The resolved packages are among the hash inputs as "package:<id>/<version>", transitive ones too.
    public static TestReport ReportOf(PlanEntry entry)
    {
        bool References(string package) => entry.OwnHashInputs.Any(input =>
            input.StartsWith($"package:{package}/", StringComparison.OrdinalIgnoreCase));

        return References("Microsoft.Testing.Extensions.TrxReport") ? TestReport.Trx
            : References("xunit.v3.core") || References("xunit.v3.core.mtp-v1") || References("xunit.v3.core.mtp-v2") ? TestReport.XunitTrx
            : TestReport.None;
    }

    public void Dispose()
    {
        if (_evaluator is not null)
            DisposeEvaluator();
    }

    // Kept out of line so the JIT cannot inline Microsoft.Build code into a caller compiled before
    // registration, or into Dispose on a run that never registered.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private IReadOnlyList<EvaluatedProject> EvaluateRegistered(IReadOnlyList<string> relativePaths, string configuration) =>
        [.. (_evaluator ??= new MsBuildProjectEvaluator(repositoryRoot)).EvaluateAll(relativePaths, configuration).Values];

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void DisposeEvaluator() => _evaluator!.Dispose();

    // MSBuild's own output is folded away; its errors are reported once it ends, where they are the
    // first thing a failed step shows. The step names match the binary logs, so a heavy step leads
    // straight to its .binlog.
    private static ProcessResult Run(
        string step, string title, StagePhase phase, PipelineOptions options, PipelineOutput output, StageRecorder stage, Func<MsBuildLogs, ProcessResult> run)
    {
        var logs = options.MsBuildLogs(step);
        ProcessResult result;
        using (output.Section(title, collapse: true))
        using (stage.Measure(phase))
            result = run(logs);

        stage.AddProcess(result.Usage);
        output.WriteLine($"resources: {step} {result.Usage}");
        if (result.ExitCode != 0)
            MsBuildErrors.Report(logs.ErrorLogPath, options.RepositoryRoot, output);

        return result;
    }

    private static Deployment DeploymentOf(PlanEntry entry)
    {
        var outputType = entry.Properties.GetValueOrDefault(OutputTypeProperty, "");
        var isExecutable = outputType.Equals("Exe", StringComparison.OrdinalIgnoreCase)
            || outputType.Equals("WinExe", StringComparison.OrdinalIgnoreCase);

        var isTool = IsTrue(entry.Properties.GetValueOrDefault(PackAsToolProperty));

        // An unpublishable executable is not packed either: the SDK leaves IsPackable on by
        // default, so falling through would ship it as a package instead.
        return (isExecutable && !isTool) switch
        {
            true when IsFalse(entry.Properties.GetValueOrDefault(IsPublishableProperty)) => Deployment.None,
            true => Deployment.Publish,
            false when IsTrue(entry.Properties.GetValueOrDefault(IsPackableProperty)) => Deployment.Pack,
            false => Deployment.None,
        };
    }

    private static bool IsFalse(string? value) => string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);

    private static bool IsTrue(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private sealed record DeploymentStep(string Kind, string Name, string Title, StagePhase Phase, Func<MsBuildLogs, ProcessResult> Run);

    private enum Deployment
    {
        None,
        Pack,
        Publish,
    }
}
