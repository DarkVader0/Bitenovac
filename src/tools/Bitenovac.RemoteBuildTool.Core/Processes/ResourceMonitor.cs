using System.Diagnostics;

namespace Bitenovac.RemoteBuildTool.Core.Processes;

/// <summary>
/// Samples what a running process uses at a fixed interval and reports the totals when it stops.
/// </summary>
/// <remarks>
/// Memory is the highest of the samples, so a peak shorter than the interval can be missed;
/// processor time is the last sample, so the work of the final interval can be. Both are close
/// enough to tell a heavy step from a light one.
/// </remarks>
public sealed class ResourceMonitor : IDisposable
{
    private readonly Func<ProcessTreeSample?> _sample;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly Lock _gate = new();
    private readonly Timer _timer;

    private long? _peakMemory;
    private TimeSpan? _cpuTime;
    private ResourceUsage? _usage;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceMonitor"/> class and takes the first sample.
    /// </summary>
    /// <param name="sample">Reads what the process uses now, or <see langword="null"/> when that cannot be read.</param>
    /// <param name="interval">How often to sample.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sample"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> is not positive.</exception>
    public ResourceMonitor(Func<ProcessTreeSample?> sample, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);

        _sample = sample;
        Take();
        _timer = new Timer(_ => Take(), null, interval, interval);
    }

    /// <summary>
    /// Stops sampling and returns what the process used.
    /// </summary>
    /// <returns>The time since the monitor started and the totals of its samples.</returns>
    /// <remarks>Later calls return the same usage.</remarks>
    public ResourceUsage Stop()
    {
        _timer.Dispose();
        lock (_gate)
        {
            _usage ??= new ResourceUsage(_stopwatch.Elapsed, _cpuTime, _peakMemory);
            return _usage;
        }
    }

    /// <inheritdoc/>
    public void Dispose() => Stop();

    /// <summary>
    /// Takes one sample now; the timer calls this at every interval.
    /// </summary>
    internal void Take()
    {
        var sample = _sample();
        lock (_gate)
        {
            if (_usage is not null || sample is not { } now)
                return;

            _peakMemory = Math.Max(_peakMemory ?? 0, now.ResidentBytes);
            _cpuTime = _cpuTime is { } before && before > now.CpuTime ? before : now.CpuTime;
        }
    }
}
