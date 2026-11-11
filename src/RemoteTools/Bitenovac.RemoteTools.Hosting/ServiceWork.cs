using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bitenovac.RemoteTools.Hosting;

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
    private static readonly Action<ILogger, string, string, Exception?> Failed =
        LoggerMessage.Define<string, string>(LogLevel.Warning, default, "{Work} failed, retried on its next turn: {Message}");

    private static readonly Action<ILogger, string, Exception?> Reported =
        LoggerMessage.Define<string>(LogLevel.Information, default, "{Message}");

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
            or InvalidOperationException
            || IsRecoverable(exception)
            || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            Failed(logger, Name, exception.Message, null);
        }
    }

    /// <summary>
    /// Returns whether a failure of a kind only this service knows is reported and retried, like
    /// the network and file failures every service shares.
    /// </summary>
    /// <param name="exception">What a turn threw.</param>
    /// <returns><see langword="true"/> when the work should carry on with its next turn.</returns>
    protected abstract bool IsRecoverable(Exception exception);

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
    /// Waits until the work is asked for a turn before its wait ends; never, unless overridden.
    /// </summary>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>A task that completes once a turn is asked for.</returns>
    protected virtual Task WokenAsync(CancellationToken cancellationToken) => Task.Delay(Timeout.Infinite, cancellationToken);

    /// <summary>
    /// Reports what the work did.
    /// </summary>
    /// <param name="message">What it did.</param>
    protected void Report(string message) => Reported(logger, message, null);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (true)
            {
                await RunTurnAsync(stoppingToken);
                await WaitForNextTurnAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task WaitForNextTurnAsync(CancellationToken stoppingToken)
    {
        var interval = await IntervalAsync(stoppingToken);
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        await Task.WhenAny(Task.Delay(interval, time, waiting.Token), WokenAsync(waiting.Token));
        await waiting.CancelAsync();
        stoppingToken.ThrowIfCancellationRequested();
    }
}
