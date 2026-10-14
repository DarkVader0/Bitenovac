namespace Bitenovac.RemoteBuildTool.Telemetry;

/// <summary>
/// Represents one build: a workflow run attempt, summed up over its stages and jobs.
/// </summary>
internal sealed record BuildInfo
{
    public int SchemaVersion { get; init; } = TelemetrySchema.Version;
    public required string BuildId { get; init; }
    public required string Queue { get; init; }
    public required string Pool { get; init; }
    public string? Repository { get; init; }
    public string? Ref { get; init; }
    public string? Sha { get; init; }
    public int? PullRequest { get; init; }
    public required string Result { get; init; }
    public DateTimeOffset? CreatedTime { get; init; }
    public DateTimeOffset? StartTime { get; init; }
    public required DateTimeOffset EndTime { get; init; }
    public long? QueueTimeMS { get; init; }
    public long? PreparationTimeMS { get; init; }
    public long? E2ETimeMS { get; init; }
    public int Jobs { get; init; }
    public int Stages { get; init; }
    public long StageTimeMS { get; init; }
    public long? CpuTimeMS { get; init; }
    public long? AllocatedCpuMS { get; init; }
    public double? CpuUtilization { get; init; }
    public long? PeakMemoryBytes { get; init; }
    public double? MemoryUtilization { get; init; }
    public double? HostCpuUtilization { get; init; }
    public double? HostMemoryUtilization { get; init; }
    public int MachinesUsed { get; init; }
    public int? CacheHits { get; init; }
    public int? CacheMisses { get; init; }
    public int? TestsFailed { get; init; }
    public string? FirstError { get; init; }
    public string? LogsPath { get; init; }
    public string? DropVersion { get; init; }
}
