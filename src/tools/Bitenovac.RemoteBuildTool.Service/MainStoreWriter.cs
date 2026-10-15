using Bitenovac.RemoteBuildTool.Commands;

namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Represents the one writer of main: promotions of merged pull requests and the daily switch to a
/// rebuild, one at a time.
/// </summary>
/// <remarks>
/// A rebuild builds the branch as it was when it started; pull requests merged while it ran are
/// promoted into main as usual, and promoted again once main is switched, so the switch never
/// takes their output back out.
/// </remarks>
/// <param name="time">The clock.</param>
internal sealed class MainStoreWriter(TimeProvider time)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<(DateTimeOffset At, PipelineOptions Run)> _promotions = [];

    /// <summary>
    /// Promotes one run's staged output into main.
    /// </summary>
    /// <param name="run">The run's options: its staged store, its plan in it, and main.</param>
    /// <param name="cancellationToken">Stops the wait for another write to finish.</param>
    /// <returns>The promotion's last line, as in <c>Promoted 3 entries into main.</c></returns>
    public async Task<string> PromoteAsync(PipelineOptions run, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var now = time.GetUtcNow();
            _promotions.RemoveAll(promotion => now - promotion.At > StagingRetention.Retention);
            _promotions.Add((now, run));
            return Promote(run, out _);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Switches main to a rebuild, then promotes again what was promoted since it started.
    /// </summary>
    /// <param name="rebuild">The rebuild's options: its own store, its plan in it, and main.</param>
    /// <param name="output">Where to report.</param>
    /// <param name="since">When the rebuild started.</param>
    /// <param name="cancellationToken">Stops the wait for another write to finish.</param>
    /// <returns>The switch's exit code.</returns>
    /// <exception cref="InvalidOperationException">The rebuild cannot replace main; main is left as it was.</exception>
    public async Task<int> SwitchAsync(PipelineOptions rebuild, PipelineOutput output, DateTimeOffset since, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var exitCode = SwitchCommand.Run(rebuild, output);
            foreach (var (_, run) in _promotions.Where(promotion => promotion.At >= since))
            {
                var summary = Promote(run, out var exitCodeOfRun);
                output.WriteLine($"Promoted run {run.Identity?.BuildId} again, merged while the rebuild ran: {summary}");
                exitCode = Math.Max(exitCode, exitCodeOfRun);
            }

            return exitCode;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string Promote(PipelineOptions run, out int exitCode)
    {
        using var log = new StringWriter();
        exitCode = PromoteCommand.Run(run, new PipelineOutput(log, log));
        return log.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[^1];
    }
}
