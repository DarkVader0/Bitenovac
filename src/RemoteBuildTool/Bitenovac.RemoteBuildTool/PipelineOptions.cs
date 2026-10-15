using Bitenovac.RemoteBuildTool.MsBuild;
using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool;

/// <summary>
/// Represents the locations of this run's inputs and artifact volumes, and the switches that
/// change its behavior.
/// </summary>
internal sealed record PipelineOptions(
    string RepositoryRoot,
    string MainStoreRoot,
    string PrStoreRoot,
    string DropRoot,
    bool Cacheless,
    bool CoverageHtml,
    string? LogsRoot = null,
    string? TelemetryRoot = null,
    BuildIdentity? Identity = null)
{
    public static readonly string[] Configurations = ["Debug", "Release"];

    public static PipelineOptions FromEnvironment() => FromEnvironment(Environment.GetEnvironmentVariable);

    /// <summary>
    /// Reads the options from <c>REMOTEBUILDTOOL_*</c> variables; an empty one counts as unset.
    /// </summary>
    /// <param name="read">Returns a variable's value, or <see langword="null"/> when it is unset.</param>
    /// <returns>The options.</returns>
    public static PipelineOptions FromEnvironment(Func<string, string?> read)
    {
        string? Value(string name) => read(name) is { Length: > 0 } value ? value : null;

        var repositoryRoot = Value("REMOTEBUILDTOOL_REPO_ROOT") ?? Directory.GetCurrentDirectory();
        return new PipelineOptions(
            repositoryRoot,
            Value("REMOTEBUILDTOOL_MAIN_STORE") ?? Path.Combine(repositoryRoot, "artifacts", "ci-main"),
            Value("REMOTEBUILDTOOL_PR_STORE") ?? Path.Combine(repositoryRoot, "artifacts", "ci-pr"),
            Value("REMOTEBUILDTOOL_DROP_ROOT") ?? Path.Combine(repositoryRoot, "artifacts", "official"),
            IsTrue(read("REMOTEBUILDTOOL_CACHELESS")),
            read("REMOTEBUILDTOOL_COVERAGE_HTML") != "0",
            Value("REMOTEBUILDTOOL_LOGS_DIR"),
            Value("REMOTEBUILDTOOL_TELEMETRY_DIR"),
            BuildIdentity.FromEnvironment(read));
    }

    public string PlanFile => Path.Combine(PrStoreRoot, "plan.json");

    public string SyntheticSolutionPath(string stage) =>
        Path.Combine(PrStoreRoot, $"{stage}.slnx");

    public string RestoreOutputsRoot => Path.Combine(PrStoreRoot, "restore");

    /// <summary>
    /// Returns where one MSBuild invocation writes its binary log.
    /// </summary>
    /// <param name="name">The name of the invocation, unique within the run; for example <c>build-Debug</c>.</param>
    /// <returns>
    /// The path of <c>&lt;name&gt;.binlog</c> under <see cref="LogsRoot"/>, or <see langword="null"/>
    /// when the run keeps no logs.
    /// </returns>
    public string? BinaryLogPath(string name) =>
        LogsRoot is null ? null : Path.Combine(LogsRoot, $"{name}.binlog");

    /// <summary>
    /// Returns where one MSBuild invocation writes its logs.
    /// </summary>
    /// <param name="name">The name of the invocation, unique within the run; for example <c>build-Debug</c>.</param>
    /// <returns>
    /// The binary log of <see cref="BinaryLogPath"/>, and <c>msbuild/&lt;name&gt;.errors.log</c> and
    /// <c>msbuild/&lt;name&gt;.performance.log</c> in this run's own volume for its errors and timings.
    /// </returns>
    public MsBuildLogs MsBuildLogs(string name) =>
        new(BinaryLogPath(name), Path.Combine(PrStoreRoot, "msbuild", $"{name}.errors.log"), Path.Combine(PrStoreRoot, "msbuild", $"{name}.performance.log"));

    /// <summary>
    /// Returns the number of processes and MSBuild nodes that one job may run at once.
    /// </summary>
    /// <returns>
    /// The value of <c>REMOTEBUILDTOOL_MAX_CPU</c> if it is a positive integer; otherwise, the
    /// number of processors.
    /// </returns>
    public static int MaxParallelism() =>
        int.TryParse(Environment.GetEnvironmentVariable("REMOTEBUILDTOOL_MAX_CPU"), out var maxCpu) && maxCpu > 0
            ? maxCpu
            : Environment.ProcessorCount;

    public string CoverageDirectory(string configuration) =>
        Path.Combine(RepositoryRoot, "artifacts", "coverage", configuration);

    public string CoverageReportDirectory(string configuration) =>
        Path.Combine(RepositoryRoot, "artifacts", "coverage-report", configuration);

    private static bool IsTrue(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
}
