using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Provides a method for recording the builds GitHub ran: a <see cref="BuildInfo"/> for every
/// completed run whose stages reached the telemetry share, with its queue and job times from GitHub.
/// </summary>
/// <param name="github">The GitHub API.</param>
/// <param name="host">The machine's settings.</param>
internal sealed class BuildReporter(GitHubClient github, HostSettings host)
{
    private const int RememberedBuilds = 5000;

    private static readonly TimeSpan LookBack = TimeSpan.FromDays(2);

    /// <summary>
    /// Records every completed run of the last two days not recorded yet.
    /// </summary>
    /// <param name="reported">The builds already recorded.</param>
    /// <param name="now">The current time.</param>
    /// <param name="cancellationToken">Stops the requests.</param>
    /// <returns>The builds recorded now and before, the newest last.</returns>
    /// <remarks>
    /// A run without RemoteBuildTool stages, such as a deployment, is remembered but writes nothing.
    /// </remarks>
    /// <exception cref="HttpRequestException">GitHub refused or could not be reached.</exception>
    public async Task<IReadOnlyList<string>> ReportAsync(IReadOnlyList<string> reported, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var known = reported.ToHashSet(StringComparer.Ordinal);
        var recorded = reported.ToList();

        foreach (var run in (await github.CompletedRunsAsync(now - LookBack, cancellationToken)).OrderBy(run => run.UpdatedAt))
        {
            if (!known.Add(run.BuildId))
                continue;

            var stage = TelemetryWriter.Read(host.TelemetryRoot, TelemetryWriter.StageTable, run.BuildId + ".", TelemetryJsonContext.Default.StageInfo)
                .FirstOrDefault();
            if (stage is not null)
            {
                var jobs = WorkflowJob.Parse(await github.JobsAsync(run, cancellationToken));
                var identity = new BuildIdentity(
                    run.BuildId,
                    run.Workflow,
                    stage.Pool,
                    stage.Machine,
                    Repository: host.Repository,
                    Ref: stage.Ref ?? run.HeadBranch,
                    Sha: stage.Sha ?? run.HeadSha,
                    PullRequest: stage.PullRequest ?? run.PullRequest,
                    LogsPath: stage.LogsPath);
                BuildSummarizer.Write(host.TelemetryRoot, identity, run.Conclusion, run.UpdatedAt, BuildTimeline.FromJobs(jobs, run.UpdatedAt), host.EffectiveMachine);
            }

            recorded.Add(run.BuildId);
        }

        return recorded.Count > RememberedBuilds ? recorded[^RememberedBuilds..] : recorded;
    }
}
