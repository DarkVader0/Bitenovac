using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Planning;
using Bitenovac.RemoteBuildTool.Telemetry;
using Bitenovac.RemoteBuildTool.Testing;
using Bitenovac.RemoteBuildTool.Toolchains;
using Bitenovac.RemoteBuildTool.Toolchains.DotNet;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

/// <summary>
/// Provides the temporary directories, plan entries and options shared by the RemoteBuildTool tests.
/// </summary>
internal static class TestFactory
{
    public static ProjectId Id(string relativePath) => new(relativePath);

    /// <summary>
    /// Creates an output that discards everything written to it.
    /// </summary>
    public static PipelineOutput Silence() => new(TextWriter.Null, TextWriter.Null);

    public static TemporaryDirectory Directory() => new();

    /// <summary>
    /// Writes <paramref name="content"/> to <paramref name="path"/>, creating its directory.
    /// </summary>
    public static string WriteFile(string path, string content)
    {
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>
    /// Writes <paramref name="content"/> to <paramref name="relativePath"/> under <paramref name="root"/>.
    /// </summary>
    public static string WriteFile(string root, string relativePath, string content) =>
        WriteFile(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)), content);

    public static PipelineOptions Options(
        string repositoryRoot,
        string mainStoreRoot,
        string prStoreRoot,
        bool cacheless = false,
        bool coverageHtml = false) =>
        new(repositoryRoot, mainStoreRoot, prStoreRoot, Path.Combine(repositoryRoot, "artifacts", "official"), cacheless, coverageHtml);

    public static PlanEntry Entry(
        string projectPath,
        string fullPath = @"C:\repo\Project.csproj",
        string toolchain = DotNetToolchain.ToolchainName,
        IReadOnlyList<string>? references = null,
        string coverageName = "Project",
        IReadOnlyDictionary<string, string>? properties = null,
        bool isTestProject = false,
        bool excludeFromCoverage = false,
        double minimumLineCoverage = 100,
        double minimumBranchCoverage = 100,
        bool cacheTestResults = true,
        string ownHash = "own-hash",
        string fullHash = "full-hash",
        bool forced = false,
        bool hit = false,
        bool shouldGateCoverage = true,
        IReadOnlyList<string>? ownHashInputs = null,
        string? whyCacheMiss = null) =>
        new(
            projectPath,
            fullPath,
            toolchain,
            references ?? [],
            coverageName,
            properties ?? new Dictionary<string, string>(),
            isTestProject,
            excludeFromCoverage,
            minimumLineCoverage,
            minimumBranchCoverage,
            cacheTestResults,
            ownHash,
            fullHash,
            forced,
            hit,
            shouldGateCoverage,
            ownHashInputs ?? [],
            whyCacheMiss);

    public static PlanState Plan(string configuration, params PlanEntry[] entries) =>
        new(new Dictionary<string, List<PlanEntry>> { [configuration] = [.. entries] });

    public static BuildIdentity Identity(string buildId = "100-1", string machine = "host-1") =>
        new(buildId, "PR", "docker", machine, "plan", "owner/repo", "refs/pull/7/merge", "abc123", 7, "PR_7/100-1");

    public static MachineCounters Counters(
        long hostBusy = 0,
        long hostTotal = 0,
        double containerCpuSeconds = 0,
        long containerPeak = 0,
        long containerLimit = 0,
        double cpuLimit = 1) =>
        new(hostBusy, hostTotal, 8L << 30, 6L << 30, TimeSpan.FromSeconds(containerCpuSeconds), containerPeak / 2, containerPeak, containerLimit, cpuLimit);

    public static PipelineOptions TelemetryOptions(TemporaryDirectory directory, BuildIdentity? identity = null) =>
        Options(directory.Path, directory.Combine("main"), directory.Combine("pr")) with
        {
            TelemetryRoot = directory.Combine("telemetry"),
            Identity = identity ?? Identity(),
        };
}

/// <summary>
/// Represents the <c>REMOTEBUILDTOOL_*</c> environment variables of one test, restored to their
/// previous values on disposal.
/// </summary>
internal sealed class EnvironmentVariables : IDisposable
{
    /// <summary>
    /// The name of the xUnit collection that every class writing these variables belongs to.
    /// </summary>
    public const string Collection = "Environment variables";

    private static readonly string[] Names =
    [
        "REMOTEBUILDTOOL_REPO_ROOT",
        "REMOTEBUILDTOOL_MAIN_STORE",
        "REMOTEBUILDTOOL_PR_STORE",
        "REMOTEBUILDTOOL_DROP_ROOT",
        "REMOTEBUILDTOOL_CACHELESS",
        "REMOTEBUILDTOOL_COVERAGE_HTML",
        "REMOTEBUILDTOOL_MAX_CPU",
        "REMOTEBUILDTOOL_LOGS_DIR",
        "REMOTEBUILDTOOL_TELEMETRY_DIR",
        "REMOTEBUILDTOOL_CONFIG",
    ];

    private readonly Dictionary<string, string?> _original =
        Names.ToDictionary(name => name, Environment.GetEnvironmentVariable);

    public void Clear()
    {
        foreach (var name in Names)
            Environment.SetEnvironmentVariable(name, null);
    }

    public void Set(string name, string? value) => Environment.SetEnvironmentVariable(name, value);

    public void Dispose()
    {
        foreach (var (name, value) in _original)
            Environment.SetEnvironmentVariable(name, value);
    }
}

/// <summary>
/// Represents a directory under the system temporary folder that is deleted on disposal.
/// </summary>
internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory() =>
        Path = System.IO.Directory.CreateTempSubdirectory("bitenovac-remotebuildtool-tests-").FullName;

    public string Path { get; }

    /// <summary>
    /// Returns the path of <paramref name="relativePath"/> under this directory, accepting forward slashes.
    /// </summary>
    public string Combine(string relativePath) =>
        System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public void Dispose()
    {
        if (!System.IO.Directory.Exists(Path))
            return;

        foreach (var file in System.IO.Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);

        System.IO.Directory.Delete(Path, recursive: true);
    }
}

/// <summary>
/// Represents the output a command reported, captured without touching the console.
/// </summary>
internal sealed class CapturedOutput
{
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public PipelineOutput Pipeline => new(_out, _error);

    public string Out => _out.ToString();

    public string Error => _error.ToString();
}

/// <summary>
/// Represents a toolchain named like the .NET one that builds, publishes and tests nothing, and
/// reports what each call was told to.
/// </summary>
internal sealed class FakeToolchain : IToolchain
{
    public int BuildExitCode { get; init; }

    public int PublishExitCode { get; init; }

    /// <summary>
    /// Gets the result of running a test project, given its entry and the results directory.
    /// </summary>
    public Func<PlanEntry, string, TestRunResult> Tests { get; init; } =
        (_, directory) => new TestRunResult(TestRunOutcome.Passed, 0, directory, "");

    public List<string> Built { get; } = [];

    public string Name => DotNetToolchain.ToolchainName;

    public IReadOnlyList<string> OutputDirectories => ["bin", "obj"];

    public IReadOnlyList<string> Discover() => [];

    public IReadOnlyList<EvaluatedProject> Evaluate(IReadOnlyList<string> relativePaths, string configuration) => [];

    public ToolchainPreparation Prepare(
        PipelineOptions options,
        IReadOnlyDictionary<string, IReadOnlyList<EvaluatedProject>> byConfiguration,
        PipelineOutput output,
        StageRecorder stage) =>
        throw new NotSupportedException();

    public int Build(PipelineOptions options, IReadOnlyList<PlanEntry> entries, string configuration, PipelineOutput output, StageRecorder stage)
    {
        Built.AddRange(entries.Select(entry => entry.ProjectPath));
        return BuildExitCode;
    }

    public int Publish(PipelineOptions options, IReadOnlyList<PlanEntry> entries, string version, string dropDirectory, PipelineOutput output, StageRecorder stage)
    {
        File.WriteAllText(Path.Combine(dropDirectory, "published.txt"), version);
        return PublishExitCode;
    }

    public TestRunResult RunTests(PlanEntry entry, string configuration, string resultsFilePrefix, string resultsDirectory) =>
        Tests(entry, resultsDirectory);

    public ToolchainRegistry Registry() => new([this]);

    public void Dispose()
    {
    }
}
