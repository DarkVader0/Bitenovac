namespace Bitenovac.RemoteBuildTool.Telemetry;

/// <summary>
/// Samples the machine and the stage's container while a stage runs: once when it starts, every
/// interval after that, and once when it stops.
/// </summary>
internal sealed class MachineSampler
{
    private readonly BuildIdentity _identity;
    private readonly string _stage;
    private readonly Func<MachineCounters> _readCounters;
    private readonly List<MachineSample> _samples = [];
    private readonly Lock _gate = new();
    private readonly Timer _timer;
    private MachineCounters _last;
    private bool _stopped;

    /// <summary>
    /// Initializes a new instance of the <see cref="MachineSampler"/> class and takes the first sample.
    /// </summary>
    /// <param name="identity">The build the samples belong to.</param>
    /// <param name="stage">The stage's name, as in <c>build-Debug</c>.</param>
    /// <param name="interval">How often to sample.</param>
    /// <param name="readCounters">Reads the machine's counters.</param>
    public MachineSampler(BuildIdentity identity, string stage, TimeSpan interval, Func<MachineCounters> readCounters)
    {
        _identity = identity;
        _stage = stage;
        _readCounters = readCounters;
        _last = readCounters();
        Add(_last, _last);
        _timer = new Timer(_ => SampleNow(), null, interval, interval);
    }

    /// <summary>
    /// Takes the last sample and stops sampling.
    /// </summary>
    /// <returns>The counters at the end, and every sample taken.</returns>
    public (MachineCounters Counters, IReadOnlyList<MachineSample> Samples) Stop()
    {
        _timer.Dispose();
        lock (_gate)
        {
            _stopped = true;
            var counters = _readCounters();
            Add(_last, counters);
            return (counters, [.. _samples]);
        }
    }

    /// <summary>
    /// Takes a sample, as the timer does every interval; once stopped, takes none.
    /// </summary>
    public void SampleNow()
    {
        lock (_gate)
        {
            if (_stopped)
                return;

            var counters = _readCounters();
            Add(_last, counters);
            _last = counters;
        }
    }

    private void Add(MachineCounters earlier, MachineCounters now) =>
        _samples.Add(new MachineSample
        {
            BuildId = _identity.BuildId,
            Queue = _identity.Queue,
            Pool = _identity.Pool,
            Machine = _identity.Machine,
            Stage = _stage,
            Time = DateTimeOffset.UtcNow,
            HostCpuUtilization = now.HostCpuUtilizationSince(earlier),
            HostMemoryUsedBytes = now.HostMemoryUsedBytes,
            HostMemoryTotalBytes = now.HostMemoryTotalBytes,
            HostMemoryUtilization = (now.HostMemoryTotalBytes, now.HostMemoryAvailableBytes) is (long total and > 0, { } available) ? (double)(total - available) / total : null,
            ContainerCpuMS = now.ContainerCpuTime is { } cpu ? (long)cpu.TotalMilliseconds : null,
            ContainerMemoryBytes = now.ContainerMemoryBytes,
            ContainerMemoryLimitBytes = now.ContainerMemoryLimitBytes,
        });
}
