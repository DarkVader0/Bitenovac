namespace Bitenovac.RemoteBuildTool.Telemetry;

/// <summary>
/// Represents one stage of a build: one RemoteBuildTool command in one job.
/// </summary>
internal sealed record StageInfo
{
    public int SchemaVersion { get; init; } = TelemetrySchema.Version;
    public required string BuildId { get; init; }
    public required string Queue { get; init; }
    public required string Pool { get; init; }
    public required string Machine { get; init; }
    public string? Job { get; init; }
    public required string Stage { get; init; }
    public string? Configuration { get; init; }
    public string? Ref { get; init; }
    public string? Sha { get; init; }
    public int? PullRequest { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public required DateTimeOffset EndTime { get; init; }
    public long DurationMS { get; init; }
    public int ExitCode { get; init; }
    public bool Succeeded { get; init; }
    public string? FirstError { get; init; }
    public int? Projects { get; init; }
    public int? CacheHits { get; init; }
    public int? CacheMisses { get; init; }
    public long? EvaluationMS { get; init; }
    public long? RestoreMS { get; init; }
    public long? HashingMS { get; init; }
    public long? CacheLookupMS { get; init; }
    public long? CacheRestoreMS { get; init; }
    public long? BuildMS { get; init; }
    public long? TestMS { get; init; }
    public long? CoverageMS { get; init; }
    public long? PublishMS { get; init; }
    public long? PackMS { get; init; }
    public long? StoreMS { get; init; }
    public long? CpuTimeMS { get; init; }
    public double? AllocatedCpus { get; init; }
    public long? AllocatedCpuMS { get; init; }
    public double? CpuUtilization { get; init; }
    public long? PeakMemoryBytes { get; init; }
    public long? MemoryLimitBytes { get; init; }
    public double? MemoryUtilization { get; init; }
    public string? LogsPath { get; init; }
}
