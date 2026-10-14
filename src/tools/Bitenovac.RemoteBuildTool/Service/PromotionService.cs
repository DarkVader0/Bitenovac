using Microsoft.Extensions.Logging;

namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Represents the work done every poll: staged output older than a day is deleted, and the staged
/// output of each newly merged pull request is promoted into main.
/// </summary>
/// <param name="host">The machine's settings.</param>
/// <param name="settings">The repository's settings.</param>
/// <param name="state">What the service has done.</param>
/// <param name="promoter">Promotes merged pull requests.</param>
/// <param name="time">The clock.</param>
/// <param name="logger">Where the work reports.</param>
internal sealed class PromotionService(
    HostSettings host,
    RepositorySettingsSource settings,
    ServiceStateStore state,
    PullRequestPromoter promoter,
    TimeProvider time,
    ILogger<PromotionService> logger) : ServiceWork(time, logger)
{
    /// <inheritdoc/>
    protected override string Name => "promotion";

    /// <inheritdoc/>
    protected override async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var current = await settings.GetAsync(cancellationToken);
        var now = Time.GetUtcNow();

        foreach (var run in StagingRetention.Prune(host.StagingRoot, now))
            Report($"staged output of run {run} expired");

        var promoted = await promoter.PromoteAsync(current.Branch, state.Current.PromotedPullRequests, now, Report, cancellationToken);
        state.Update(saved => saved with { PromotedPullRequests = promoted });
    }

    /// <inheritdoc/>
    protected override async Task<TimeSpan> IntervalAsync(CancellationToken cancellationToken) =>
        TimeSpan.FromSeconds((await settings.GetAsync(cancellationToken)).PollSeconds);
}
