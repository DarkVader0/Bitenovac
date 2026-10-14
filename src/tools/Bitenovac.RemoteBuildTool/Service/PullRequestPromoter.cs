using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Provides a method for filling main from merged pull requests: the output their own build
/// staged, promoted once GitHub reports them merged.
/// </summary>
/// <remarks>
/// <para>
/// Every PR and merge queue run keeps its artifact store in the staging store, one folder per run
/// attempt. A merged pull request's run is looked up by commit: a merge queue run on the merge
/// commit first, as it built exactly what the branch now holds; otherwise the pull request's own
/// run on its last commit. Its entries are keyed by fingerprint, so an entry for code the branch
/// does not hold, as when the branch moved before the merge, is never a hit, only unused.
/// </para>
/// <para>
/// Only merged code enters main: a run is promoted for a merged pull request alone, and its
/// staged output is deleted a day after the run, merged or not; see <see cref="StagingRetention"/>.
/// </para>
/// </remarks>
/// <param name="github">The GitHub API.</param>
/// <param name="host">The machine's settings.</param>
/// <param name="writer">Writes main, one write at a time.</param>
internal sealed class PullRequestPromoter(GitHubClient github, HostSettings host, MainStoreWriter writer)
{
    private const int RememberedPullRequests = 5000;

    /// <summary>
    /// Promotes every pull request merged into <paramref name="branch"/> within the staging
    /// retention and not promoted yet.
    /// </summary>
    /// <param name="branch">The branch main follows.</param>
    /// <param name="handled">The pull requests already handled.</param>
    /// <param name="now">The current time.</param>
    /// <param name="report">Where to report what was promoted.</param>
    /// <param name="cancellationToken">Stops the requests.</param>
    /// <returns>The pull requests handled now and before, the newest last.</returns>
    /// <exception cref="HttpRequestException">GitHub refused or could not be reached.</exception>
    public async Task<IReadOnlyList<int>> PromoteAsync(
        string branch, IReadOnlyList<int> handled, DateTimeOffset now, Action<string> report, CancellationToken cancellationToken)
    {
        var known = handled.ToHashSet();
        var recorded = handled.ToList();
        var merged = (await github.MergedPullRequestsAsync(branch, cancellationToken))
            .Where(pull => now - pull.MergedAt <= StagingRetention.Retention && !known.Contains(pull.Number))
            .OrderBy(pull => pull.MergedAt);

        foreach (var pull in merged)
        {
            var store = await StagedStoreAsync(pull, cancellationToken);
            if (store is null)
            {
                report($"PR #{pull.Number} merged; no successful run of it is staged, so nothing to promote");
            }
            else
            {
                var summary = await writer.PromoteAsync(Options(store.Value.Run, store.Value.Directory, pull), cancellationToken);
                report($"PR #{pull.Number} merged; run {store.Value.Run.BuildId}: {summary}");
            }

            recorded.Add(pull.Number);
        }

        return recorded.Count > RememberedPullRequests ? recorded[^RememberedPullRequests..] : recorded;
    }

    private async Task<(WorkflowRun Run, string Directory)?> StagedStoreAsync(MergedPullRequest pull, CancellationToken cancellationToken)
    {
        var candidates = new List<WorkflowRun>();
        if (pull.MergeCommitSha is { Length: > 0 } merge)
            candidates.AddRange((await github.SuccessfulRunsAsync(merge, cancellationToken)).Where(run => run.Event == "merge_group"));
        candidates.AddRange((await github.SuccessfulRunsAsync(pull.HeadSha, cancellationToken)).Where(run => run.Event == "pull_request"));

        return candidates
            .Select(run => (Run: run, Directory: Path.Combine(host.StagingRoot, run.BuildId)))
            .Where(candidate => File.Exists(Path.Combine(candidate.Directory, "plan.json")))
            .Select(candidate => ((WorkflowRun, string)?)candidate)
            .FirstOrDefault();
    }

    private PipelineOptions Options(WorkflowRun run, string store, MergedPullRequest pull) => new(
        RepositoryRoot: store,
        MainStoreRoot: host.MainStore,
        PrStoreRoot: store,
        DropRoot: store,
        Cacheless: false,
        CoverageHtml: false,
        TelemetryRoot: host.TelemetryRoot,
        Identity: new BuildIdentity(run.BuildId, run.Workflow, host.Pool, host.EffectiveMachine, Repository: host.Repository, Sha: pull.HeadSha, PullRequest: pull.Number));
}

/// <summary>
/// Provides a method for deleting the staged output of runs older than a day.
/// </summary>
internal static class StagingRetention
{
    /// <summary>
    /// How long a run's staged output is kept, and so how long after its run a pull request can be
    /// merged and still fill main.
    /// </summary>
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    /// <summary>
    /// Deletes every run folder the run last wrote more than <see cref="Retention"/> ago.
    /// </summary>
    /// <param name="stagingRoot">The staging store.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The run folders deleted.</returns>
    public static IReadOnlyList<string> Prune(string stagingRoot, DateTimeOffset now)
    {
        if (!Directory.Exists(stagingRoot))
            return [];

        var expired = new DirectoryInfo(stagingRoot).EnumerateDirectories()
            .Where(run => now - run.LastWriteTimeUtc > Retention)
            .ToList();

        foreach (var run in expired)
            run.Delete(recursive: true);

        return [.. expired.Select(run => run.Name)];
    }
}
