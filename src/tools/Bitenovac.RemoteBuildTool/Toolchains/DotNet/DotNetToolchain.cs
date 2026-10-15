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

        var verifyExitCode = Verify(debug, output);
        if (verifyExitCode != 0)
            return ToolchainPreparation.Failed(verifyExitCode);

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
        Build(options, entries, configuration, new Dictionary<string, string>(), output, stage);

    public int Publish(PipelineOptions options, IReadOnlyList<PlanEntry> entries, string version, string dropDirectory, PipelineOutput output, StageRecorder stage)
    {
        // VersionPrefix, not Version: SDK targets derive Version from VersionPrefix only while Version
        // is unset, so a release Version would mask a project's own VersionPrefix.
        var defaultProperties = new Dictionary<string, string> { ["VersionPrefix"] = version };

        var exitCode = Build(options, entries, "Release", defaultProperties, output, stage);
        if (exitCode != 0)
            return exitCode;

        foreach (var entry in entries)
        {
            var name = Path.GetFileNameWithoutExtension(entry.ProjectPath);

            var (kind, result) = DeploymentOf(entry) switch
            {
                Deployment.Publish => ("publish", Run($"publish-{name}", $"{name}: publish", StagePhase.Publish, options, output, stage, logs =>
                    msBuild.Publish(options.RepositoryRoot, entry.FullPath, defaultProperties, Path.Combine(dropDirectory, "apps", name), logs))),
                Deployment.Pack => ("pack", Run($"pack-{name}", $"{name}: pack", StagePhase.Pack, options, output, stage, logs =>
                    msBuild.Pack(options.RepositoryRoot, entry.FullPath, defaultProperties, Path.Combine(dropDirectory, "packages"), logs))),
                _ => ((string?)null, (ProcessResult?)null),
            };

            if (kind is null || result is null)
            {
                output.WriteLine($"--- {name}: nothing to deploy");
                continue;
            }

            stage.AddTarget(new TargetInfo
            {
                Project = entry.ProjectPath,
                Toolchain = Name,
                Kind = kind,
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

        return 0;
    }

    private int Build(
        PipelineOptions options,
        IReadOnlyList<PlanEntry> entries,
        string configuration,
        IReadOnlyDictionary<string, string> defaultProperties,
        PipelineOutput output,
        StageRecorder stage)
    {
        var unrestored = entries
            .Where(entry => !RestoreOutputs.Materialise(options.RepositoryRoot, entry.FullPath, options.RestoreOutputsRoot))
            .ToList();

        if (unrestored.Count > 0)
        {
            var restoreExitCode = Run($"restore-{configuration}", $"Restoring {unrestored.Count} project(s) plan did not restore", StagePhase.Restore, options, output, stage, logs => msBuild.Restore(
                options.RepositoryRoot,
                unrestored.Select(entry => entry.FullPath),
                configuration,
                options.SyntheticSolutionPath($"restore-{configuration}"),
                logs)).ExitCode;

            if (restoreExitCode != 0)
            {
                output.WriteFailure($"restore failed (exit {restoreExitCode}).");
                return restoreExitCode;
            }
        }

        var step = $"build-{configuration}";
        var exitCode = Run(step, $"Building {entries.Count} project(s) ({configuration})", StagePhase.Build, options, output, stage, logs => msBuild.Build(
            options.RepositoryRoot,
            entries.Select(entry => entry.FullPath),
            configuration,
            options.SyntheticSolutionPath(step),
            defaultProperties,
            logs)).ExitCode;

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

    private static int Verify(IReadOnlyList<EvaluatedProject> projects, PipelineOutput output)
    {
        output.WriteLine("==> Verifying build assumptions");

        var offenders = projects
            .Where(project => !string.IsNullOrEmpty(project.Properties.GetValueOrDefault(TargetFrameworksProperty)))
            .ToList();

        if (offenders.Count > 0)
        {
            output.WriteFailure("these projects declare <TargetFrameworks>. Set a single <TargetFramework>, or change RepositoryTargetFramework in Directory.Build.props.");
            foreach (var project in offenders)
                output.WriteError($"  {project.Id}");
            return 1;
        }

        output.WriteLine("OK: every project targets a single framework.");
        return 0;
    }

    private enum Deployment
    {
        None,
        Pack,
        Publish,
    }
}
