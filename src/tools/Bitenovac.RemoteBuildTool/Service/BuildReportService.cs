using Microsoft.Extensions.Logging;

namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Represents the work done every few minutes: a build record for each build GitHub finished.
/// </summary>
/// <param name="settings">The repository's settings.</param>
/// <param name="state">What the service has done.</param>
/// <param name="reporter">Records finished builds.</param>
/// <param name="time">The clock.</param>
/// <param name="logger">Where the work reports.</param>
internal sealed class BuildReportService(
    RepositorySettingsSource settings,
    ServiceStateStore state,
    BuildReporter reporter,
    TimeProvider time,
    ILogger<BuildReportService> logger) : ServiceWork(time, logger)
{
    /// <inheritdoc/>
    protected override string Name => "build records";

    /// <inheritdoc/>
    protected override async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var reported = await reporter.ReportAsync(state.Current.ReportedBuilds, Time.GetUtcNow(), cancellationToken);
        state.Update(saved => saved with { ReportedBuilds = reported });
    }

    /// <inheritdoc/>
    protected override async Task<TimeSpan> IntervalAsync(CancellationToken cancellationToken) =>
        TimeSpan.FromSeconds((await settings.GetAsync(cancellationToken)).BuildReportSeconds);
}
