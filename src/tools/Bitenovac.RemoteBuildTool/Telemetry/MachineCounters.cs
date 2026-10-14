using System.Globalization;

namespace Bitenovac.RemoteBuildTool.Telemetry;

/// <summary>
/// Represents the host's and the stage's container's counters at one moment; any is
/// <see langword="null"/> when the system does not expose it.
/// </summary>
/// <param name="HostCpuBusyTicks">Clock ticks every host CPU spent busy since boot.</param>
/// <param name="HostCpuTotalTicks">Clock ticks every host CPU spent at all since boot.</param>
/// <param name="HostMemoryTotalBytes">The host's memory.</param>
/// <param name="HostMemoryAvailableBytes">The host's memory available without swapping.</param>
/// <param name="ContainerCpuTime">The CPU time the container used since it started.</param>
/// <param name="ContainerMemoryBytes">The container's memory now, file cache included.</param>
/// <param name="ContainerPeakMemoryBytes">The container's highest memory, file cache included.</param>
/// <param name="ContainerMemoryLimitBytes">The container's memory limit.</param>
/// <param name="ContainerCpuLimit">The CPUs the container may use, as in <c>1.5</c>.</param>
internal sealed record MachineCounters(
    long? HostCpuBusyTicks,
    long? HostCpuTotalTicks,
    long? HostMemoryTotalBytes,
    long? HostMemoryAvailableBytes,
    TimeSpan? ContainerCpuTime,
    long? ContainerMemoryBytes,
    long? ContainerPeakMemoryBytes,
    long? ContainerMemoryLimitBytes,
    double? ContainerCpuLimit)
{
    /// <summary>
    /// Reads the counters.
    /// </summary>
    /// <param name="procRoot">Where <c>/proc</c> is mounted.</param>
    /// <param name="cgroupRoot">
    /// The stage's own cgroup v2 directory. A container sees its own at <c>/sys/fs/cgroup</c>;
    /// outside one that is the whole system, which has no limits and is not read.
    /// </param>
    /// <returns>The counters.</returns>
    public static MachineCounters Read(string procRoot = "/proc", string cgroupRoot = "/sys/fs/cgroup")
    {
        var (busy, total) = ReadCpu(Path.Combine(procRoot, "stat"));
        var memory = ReadKeyValues(Path.Combine(procRoot, "meminfo"));

        var isOwnCgroup = File.Exists(Path.Combine(cgroupRoot, "cpu.max"));
        var cpu = isOwnCgroup ? ReadKeyValues(Path.Combine(cgroupRoot, "cpu.stat")) : [];

        return new MachineCounters(
            busy,
            total,
            memory.TryGetValue("MemTotal", out var memoryTotal) ? memoryTotal * 1024 : null,
            memory.TryGetValue("MemAvailable", out var available) ? available * 1024 : null,
            cpu.TryGetValue("usage_usec", out var usage) ? TimeSpan.FromMicroseconds(usage) : null,
            isOwnCgroup ? ReadNumber(Path.Combine(cgroupRoot, "memory.current")) : null,
            isOwnCgroup ? ReadNumber(Path.Combine(cgroupRoot, "memory.peak")) : null,
            isOwnCgroup ? ReadNumber(Path.Combine(cgroupRoot, "memory.max")) : null,
            isOwnCgroup ? ReadCpuLimit(Path.Combine(cgroupRoot, "cpu.max")) : null);
    }

    /// <summary>
    /// Returns the share of host CPU time spent busy between <paramref name="earlier"/> and this.
    /// </summary>
    /// <param name="earlier">Counters read before these.</param>
    /// <returns>A fraction from 0 to 1, or <see langword="null"/> when either lacks the counters or no time passed.</returns>
    public double? HostCpuUtilizationSince(MachineCounters earlier) =>
        (HostCpuBusyTicks, HostCpuTotalTicks, earlier.HostCpuBusyTicks, earlier.HostCpuTotalTicks) switch
        {
            ({ } busy, { } total, { } busyBefore, { } totalBefore) when total > totalBefore =>
                (double)(busy - busyBefore) / (total - totalBefore),
            _ => null,
        };

    /// <summary>
    /// Gets the host memory in use, total minus available.
    /// </summary>
    public long? HostMemoryUsedBytes => HostMemoryTotalBytes - HostMemoryAvailableBytes;

    // "cpu  user nice system idle iowait irq softirq steal ..." in ticks; idle and iowait are not busy.
    private static (long? Busy, long? Total) ReadCpu(string path)
    {
        if (!File.Exists(path))
            return (null, null);

        var line = File.ReadLines(path).FirstOrDefault(line => line.StartsWith("cpu ", StringComparison.Ordinal));
        if (line is null)
            return (null, null);

        var ticks = line.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .Take(8)
            .Select(value => long.Parse(value, CultureInfo.InvariantCulture))
            .ToArray();
        var total = ticks.Sum();
        var idle = ticks[3] + (ticks.Length > 4 ? ticks[4] : 0);
        return (total - idle, total);
    }

    private static Dictionary<string, long> ReadKeyValues(string path)
    {
        var values = new Dictionary<string, long>(StringComparer.Ordinal);
        if (!File.Exists(path))
            return values;

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split([' ', ':'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                values[parts[0]] = value;
        }

        return values;
    }

    // "max" means no limit.
    private static long? ReadNumber(string path) =>
        File.Exists(path) && long.TryParse(File.ReadAllText(path).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    // "<quota> <period>" in microseconds, or "max <period>" for no limit.
    private static double? ReadCpuLimit(string path)
    {
        if (!File.Exists(path))
            return null;

        var parts = File.ReadAllText(path).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2
            && long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var quota)
            && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var period)
            && period > 0
                ? (double)quota / period
                : null;
    }
}
