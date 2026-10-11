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
    string? LogsRoot = null)
{
    public static readonly string[] Configurations = ["Debug", "Release"];

    public static PipelineOptions FromEnvironment()
    {
        var repositoryRoot = Environment.GetEnvironmentVariable("REMOTEBUILDTOOL_REPO_ROOT") is { Length: > 0 } repo
            ? repo
            : Directory.GetCurrentDirectory();

        var mainStoreRoot = Environment.GetEnvironmentVariable("REMOTEBUILDTOOL_MAIN_STORE") is { Length: > 0 } main
            ? main
            : Path.Combine(repositoryRoot, "artifacts", "ci-main");

        var prStoreRoot = Environment.GetEnvironmentVariable("REMOTEBUILDTOOL_PR_STORE") is { Length: > 0 } pr
            ? pr
            : Path.Combine(repositoryRoot, "artifacts", "ci-pr");

        var dropRoot = Environment.GetEnvironmentVariable("REMOTEBUILDTOOL_DROP_ROOT") is { Length: > 0 } drop
            ? drop
            : Path.Combine(repositoryRoot, "artifacts", "official");

        var cacheless = IsTrue(Environment.GetEnvironmentVariable("REMOTEBUILDTOOL_CACHELESS"));
        var coverageHtml = Environment.GetEnvironmentVariable("REMOTEBUILDTOOL_COVERAGE_HTML") != "0";

        var logsRoot = Environment.GetEnvironmentVariable("REMOTEBUILDTOOL_LOGS_DIR") is { Length: > 0 } logs
            ? logs
            : null;

        return new PipelineOptions(repositoryRoot, mainStoreRoot, prStoreRoot, dropRoot, cacheless, coverageHtml, logsRoot);
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
