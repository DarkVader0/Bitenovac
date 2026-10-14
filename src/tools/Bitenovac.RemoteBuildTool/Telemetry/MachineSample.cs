namespace Bitenovac.RemoteBuildTool.Telemetry;

/// <summary>
/// Represents the state of the machine and the stage's container at one moment.
/// </summary>
internal sealed record MachineSample
{
    public int SchemaVersion { get; init; } = TelemetrySchema.Version;
    public required string BuildId { get; init; }
    public required string Queue { get; init; }
    public required string Pool { get; init; }
    public required string Machine { get; init; }
    public required string Stage { get; init; }
    public required DateTimeOffset Time { get; init; }
    public double? HostCpuUtilization { get; init; }
    public long? HostMemoryUsedBytes { get; init; }
    public long? HostMemoryTotalBytes { get; init; }
    public double? HostMemoryUtilization { get; init; }
    public long? ContainerCpuMS { get; init; }
    public long? ContainerMemoryBytes { get; init; }
    public long? ContainerMemoryLimitBytes { get; init; }
}
