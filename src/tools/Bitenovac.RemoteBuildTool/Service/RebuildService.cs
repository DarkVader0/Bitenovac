using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Represents the work done once a day: the branch's head built and tested from scratch, and main
/// switched to it; see <see cref="MainStoreWriter.SwitchAsync"/>.
/// </summary>
/// <remarks>
/// An empty main, as on a new machine, is rebuilt as soon as the service starts, once per start. A
/// day's rebuild runs once, whether it succeeds or not; a failed one leaves main as it was.
/// </remarks>
/// <param name="host">The machine's settings.</param>
/// <param name="settings">The repository's settings.</param>
/// <param name="state">What the service has done.</param>
/// <param name="github">The GitHub API.</param>
/// <param name="builds">Runs the rebuild.</param>
/// <param name="writer">Switches main.</param>
/// <param name="time">The clock.</param>
/// <param name="logger">Where the work reports.</param>
internal sealed class RebuildService(
    HostSettings host,
    RepositorySettingsSource settings,
    ServiceStateStore state,
    GitHubClient github,
    ServiceBuild builds,
    MainStoreWriter writer,
    TimeProvider time,
    ILogger<RebuildService> logger) : ServiceWork(time, logger)
{
    private readonly GitWorkspace _workspace = new(Path.Combine(host.WorkRoot, "checkouts", "rebuild"), host.EffectiveCloneUrl, host.GitHubToken);
    private bool _rebuiltEmptyMain;

    /// <inheritdoc/>
    protected override string Name => "rebuild";

    /// <summary>
    /// Determines whether main holds nothing yet.
    /// </summary>
    /// <param name="mainStore">Main's store.</param>
    /// <returns><see langword="true"/> when the store is missing or empty.</returns>
    public static bool IsEmpty(string mainStore) =>
        !Directory.Exists(mainStore) || !Directory.EnumerateFileSystemEntries(mainStore).Any();

    /// <inheritdoc/>
    protected override async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var current = await settings.GetAsync(cancellationToken);
        var now = Time.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var empty = IsEmpty(host.MainStore);
        var due = empty
            ? !_rebuiltEmptyMain
            : current.Rebuild is { } at && DailySchedule.IsDue(at, state.Current.RebuiltOn, now);
        if (!due)
            return;

        _rebuiltEmptyMain |= empty;

        state.Update(saved => saved with { RebuiltOn = today });
        var head = await github.BranchHeadAsync(current.Branch, cancellationToken);
        Report($"rebuild of {head} started");

        var kind = ServiceBuildKind.Rebuild((options, output, token) => writer.SwitchAsync(options, output, now, token));
        var info = await builds.RunAsync(kind, _workspace, current.Branch, head, now, cancellationToken);
        Report(string.Create(CultureInfo.InvariantCulture,
            $"rebuild of {head}: {info.Result} in {info.StageTimeMS / 1000.0:0.0} s; logs in {info.LogsPath}"));
    }

    /// <inheritdoc/>
    protected override Task<TimeSpan> IntervalAsync(CancellationToken cancellationToken) => Task.FromResult(TimeSpan.FromMinutes(1));
}
