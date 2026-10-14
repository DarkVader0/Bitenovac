using System.Globalization;
using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Specifies a build the service runs itself, on a commit of the branch main follows.
/// </summary>
/// <param name="Kind">Names the build's logs folder and id, as in <c>CacheAudit</c>.</param>
/// <param name="Queue">The queue its telemetry records, as in <c>Cache audit</c>.</param>
/// <param name="Cacheless">Whether to ignore main and build every project.</param>
/// <param name="Steps">The RemoteBuildTool commands to run in order; the first that fails ends the build.</param>
internal sealed record ServiceBuildKind(string Kind, string Queue, bool Cacheless, IReadOnlyList<IReadOnlyList<string>> Steps)
{
    /// <summary>
    /// Rebuilds one configuration of a commit from scratch and compares it with main.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The kind of build.</returns>
    public static ServiceBuildKind CacheAudit(string configuration) => new("CacheAudit", "Cache audit", Cacheless: true,
    [
        ["plan"],
        ["build", configuration],
        ["audit", configuration],
    ]);
}

/// <summary>
/// Represents how the service starts RemoteBuildTool commands: its own executable, as the
/// service itself was started.
/// </summary>
/// <param name="FileName">The program.</param>
/// <param name="PrefixArguments">Arguments before the command, such as the assembly <c>dotnet</c> runs.</param>
internal sealed record ToolCommand(string FileName, IReadOnlyList<string> PrefixArguments)
{
    /// <summary>
    /// Gets the command line that started this process.
    /// </summary>
    public static ToolCommand Self { get; } = Environment.ProcessPath is { } path && !Path.GetFileNameWithoutExtension(path).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
        ? new ToolCommand(path, [])
        : new ToolCommand("dotnet", [typeof(ToolCommand).Assembly.Location]);
}

/// <summary>
/// Provides a method for running one <see cref="ServiceBuildKind"/> on one commit.
/// </summary>
/// <param name="host">The machine's settings: stores, shares, pool.</param>
/// <param name="tool">How to start RemoteBuildTool commands.</param>
internal sealed class ServiceBuild(HostSettings host, ToolCommand tool)
{
    /// <summary>
    /// Checks out a commit and runs a build on it, writing its logs and telemetry as any build's.
    /// </summary>
    /// <param name="kind">What to run.</param>
    /// <param name="workspace">The checkout to build in.</param>
    /// <param name="branch">The branch the commit is on.</param>
    /// <param name="sha">The commit.</param>
    /// <param name="detected">When the service saw the commit, which is when the build was queued.</param>
    /// <param name="cancellationToken">Stops the build; it is recorded as cancelled.</param>
    /// <returns>The build's record.</returns>
    public async Task<BuildInfo> RunAsync(
        ServiceBuildKind kind, GitWorkspace workspace, string branch, string sha, DateTimeOffset detected, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var shortSha = sha.Length > 12 ? sha[..12] : sha;
        var buildId = $"{kind.Kind.ToLowerInvariant()}-{shortSha}-{started.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}";
        var logRun = $"{kind.Kind}_{shortSha}/{buildId}";
        var logsDirectory = Path.Combine(host.LogsRoot, kind.Kind + "_" + shortSha, buildId);
        var store = Path.Combine(host.WorkRoot, "stores", buildId);
        var identity = new BuildIdentity(buildId, kind.Queue, host.Pool, host.EffectiveMachine, null, host.Repository, $"refs/heads/{branch}", sha, null, logRun);

        var result = "success";
        try
        {
            workspace.CheckOut(sha);
            var environment = Environment(workspace.Directory, store, logsDirectory, identity, kind.Cacheless);

            foreach (var step in kind.Steps)
            {
                var exitCode = await ChildProcess.RunAsync(
                    tool.FileName,
                    [.. tool.PrefixArguments, .. step],
                    workspace.Directory,
                    environment,
                    Path.Combine(logsDirectory, string.Join('-', step) + ".log"),
                    cancellationToken);

                if (exitCode != 0)
                {
                    result = "failure";
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            result = "cancelled";
        }
        catch (InvalidOperationException exception)
        {
            result = "failure";
            Directory.CreateDirectory(logsDirectory);
            await File.AppendAllTextAsync(Path.Combine(logsDirectory, "service.log"), exception.Message + "\n", CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(store))
                Directory.Delete(store, recursive: true);
        }

        var end = DateTimeOffset.UtcNow;
        var timeline = new BuildTimeline(detected, started, (long)(started - detected).TotalMilliseconds, (long)(end - started).TotalMilliseconds, Jobs: 1);
        return BuildSummarizer.Write(host.TelemetryRoot, identity, result, end, timeline, host.EffectiveMachine);
    }

    // A build may read GITHUB_ACTIONS into what it hashes, as ContinuousIntegrationBuild does, so
    // the service's builds see the same value the pull requests' did, or no fingerprint would
    // match. Its other Actions settings are not the service's and are cleared.
    private Dictionary<string, string> Environment(string repository, string store, string logsDirectory, BuildIdentity identity, bool cacheless)
    {
        var environment = new Dictionary<string, string>
        {
            ["REMOTEBUILDTOOL_REPO_ROOT"] = repository,
            ["REMOTEBUILDTOOL_MAIN_STORE"] = host.MainStore,
            ["REMOTEBUILDTOOL_PR_STORE"] = store,
            ["REMOTEBUILDTOOL_LOGS_DIR"] = logsDirectory,
            ["REMOTEBUILDTOOL_TELEMETRY_DIR"] = host.TelemetryRoot,
            ["REMOTEBUILDTOOL_COVERAGE_HTML"] = "0",
            ["REMOTEBUILDTOOL_CACHELESS"] = cacheless ? "true" : "false",
            ["REMOTEBUILDTOOL_BUILD_ID"] = identity.BuildId,
            ["REMOTEBUILDTOOL_QUEUE"] = identity.Queue,
            ["REMOTEBUILDTOOL_POOL"] = identity.Pool,
            ["REMOTEBUILDTOOL_MACHINE"] = identity.Machine,
            ["REMOTEBUILDTOOL_LOG_RUN"] = identity.LogsPath!,
            ["GITHUB_REPOSITORY"] = identity.Repository!,
            ["GITHUB_REF"] = identity.Ref!,
            ["GITHUB_SHA"] = identity.Sha!,
            ["GITHUB_ACTIONS"] = "true",
            ["GITHUB_STEP_SUMMARY"] = "",
        };

        if (host.MaxCpu is { } maxCpu)
            environment["REMOTEBUILDTOOL_MAX_CPU"] = maxCpu.ToString(CultureInfo.InvariantCulture);

        return environment;
    }
}
