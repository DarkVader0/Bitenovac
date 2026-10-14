using System.Globalization;
using System.Text.RegularExpressions;

namespace Bitenovac.RemoteBuildTool.Telemetry;

/// <summary>
/// Represents which build a stage belongs to and where it runs, as every telemetry record names it.
/// </summary>
/// <param name="BuildId">The workflow run and its attempt, as in <c>16473829-1</c>; unique per build.</param>
/// <param name="Queue">The workflow, such as <c>PR</c> or <c>Official</c>.</param>
/// <param name="Pool">The runner pool the job ran in.</param>
/// <param name="Machine">The runner that ran the job.</param>
/// <param name="Job">The workflow job, or <see langword="null"/> outside a workflow.</param>
/// <param name="Repository">The repository, as <c>owner/name</c>.</param>
/// <param name="Ref">The Git ref the build is for.</param>
/// <param name="Sha">The commit the build is for.</param>
/// <param name="PullRequest">The pull request the build is for, if any.</param>
/// <param name="LogsPath">The build's folder in the logs share, which holds its logs and binary logs.</param>
internal sealed partial record BuildIdentity(
    string BuildId,
    string Queue,
    string Pool,
    string Machine,
    string? Job = null,
    string? Repository = null,
    string? Ref = null,
    string? Sha = null,
    int? PullRequest = null,
    string? LogsPath = null)
{
    /// <summary>
    /// Reads the identity GitHub Actions and the runner setup put in the environment.
    /// </summary>
    /// <param name="read">Returns an environment variable, or <see langword="null"/> when it is unset.</param>
    /// <returns>
    /// The identity; outside a workflow, a <c>local-</c> build of the <c>local</c> queue and pool.
    /// <c>REMOTEBUILDTOOL_BUILD_ID</c> and <c>REMOTEBUILDTOOL_QUEUE</c> name a build the service
    /// starts itself.
    /// </returns>
    public static BuildIdentity FromEnvironment(Func<string, string?> read)
    {
        string? Value(string name) => read(name) is { Length: > 0 } value ? value : null;

        var buildId = Value("REMOTEBUILDTOOL_BUILD_ID")
            ?? (Value("GITHUB_RUN_ID") is { } runId
                ? $"{runId}-{Value("GITHUB_RUN_ATTEMPT") ?? "1"}"
                : $"local-{DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}");
        var reference = Value("GITHUB_REF");
        var match = reference is null ? Match.Empty : PullRequestRef().Match(reference);
        int? pullRequest = match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;

        return new BuildIdentity(
            buildId,
            Value("REMOTEBUILDTOOL_QUEUE") ?? Value("GITHUB_WORKFLOW") ?? "local",
            Value("REMOTEBUILDTOOL_POOL") ?? "local",
            Value("REMOTEBUILDTOOL_MACHINE") ?? Environment.MachineName,
            Value("GITHUB_JOB"),
            Value("GITHUB_REPOSITORY"),
            reference,
            Value("GITHUB_SHA"),
            pullRequest,
            Value("REMOTEBUILDTOOL_LOG_RUN"));
    }

    [GeneratedRegex(@"^refs/pull/(\d+)/")]
    private static partial Regex PullRequestRef();
}
