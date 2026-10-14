using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Represents one piece of the service's work, run again and again until the service stops.
/// </summary>
/// <remarks>
/// A failure is reported and the work runs again on its next turn; it never stops the service.
/// </remarks>
/// <param name="time">The clock the wait between turns runs on.</param>
/// <param name="logger">Where the work reports.</param>
internal abstract class ServiceWork(TimeProvider time, ILogger logger) : BackgroundService
{
    /// <summary>
    /// Gets the work's name in its reports, as in <c>promotion</c>.
    /// </summary>
    protected abstract string Name { get; }

    /// <summary>
    /// Gets the clock.
    /// </summary>
    protected TimeProvider Time => time;

    /// <summary>
    /// Runs one turn of the work, reporting a failure rather than throwing it.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>A task that completes when the turn is done.</returns>
    public async Task RunTurnAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RunOnceAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or UnauthorizedAccessException
            or InvalidOperationException or DuckDB.NET.Data.DuckDBException
            || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning("{Work} failed, retried on its next turn: {Message}", Name, exception.Message);
        }
    }

    /// <summary>
    /// Runs one turn of the work.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>A task that completes when the turn is done.</returns>
    protected abstract Task RunOnceAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns how long to wait before the next turn.
    /// </summary>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>The time to wait.</returns>
    protected abstract Task<TimeSpan> IntervalAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reports what the work did.
    /// </summary>
    /// <param name="message">What it did.</param>
    protected void Report(string message) => logger.LogInformation("{Message}", message);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (true)
            {
                await RunTurnAsync(stoppingToken);
                await Task.Delay(await IntervalAsync(stoppingToken), time, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
