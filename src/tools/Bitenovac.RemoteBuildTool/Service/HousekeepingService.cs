using Bitenovac.RemoteBuildTool.Telemetry;
using Microsoft.Extensions.Logging;

namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Represents the work done once a day: telemetry compaction and log retention.
/// </summary>
/// <param name="host">The machine's settings.</param>
/// <param name="settings">The repository's settings.</param>
/// <param name="state">What the service has done.</param>
/// <param name="time">The clock.</param>
/// <param name="logger">Where the work reports.</param>
internal sealed class HousekeepingService(
    HostSettings host,
    RepositorySettingsSource settings,
    ServiceStateStore state,
    TimeProvider time,
    ILogger<HousekeepingService> logger) : ServiceWork(time, logger)
{
    /// <inheritdoc/>
    protected override string Name => "housekeeping";

    /// <inheritdoc/>
    protected override async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var current = await settings.GetAsync(cancellationToken);
        var now = Time.GetUtcNow();
        if (!DailySchedule.IsDue(current.Housekeeping, state.Current.HousekeptOn, now))
            return;

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        state.Update(saved => saved with { HousekeptOn = today });
        foreach (var day in TelemetryCompactor.Compact(host.TelemetryRoot, today))
            Report($"compacted {day}");
        foreach (var folder in LogRetention.Prune(host.LogsRoot, current.LogRetentionDays, now))
            Report($"pruned logs {folder}");
    }

    /// <inheritdoc/>
    protected override Task<TimeSpan> IntervalAsync(CancellationToken cancellationToken) => Task.FromResult(TimeSpan.FromMinutes(1));
}
