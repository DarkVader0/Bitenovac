namespace Bitenovac.RemoteBuildTool.Telemetry;

/// <summary>
/// Represents what one stage did with one project: built or restored it, or ran or reused its tests.
/// </summary>
internal sealed record TargetInfo
{
    public int SchemaVersion { get; init; } = TelemetrySchema.Version;
    public string BuildId { get; init; } = "";
    public string Queue { get; init; } = "";
    public string Pool { get; init; } = "";
    public string Machine { get; init; } = "";
    public string Stage { get; init; } = "";
    public string? Configuration { get; init; }
    public required string Project { get; init; }
    public required string Toolchain { get; init; }
    public required string Kind { get; init; }
    public string? TestKind { get; init; }
    public required string CacheOutcome { get; init; }
    public string? WhyCacheMiss { get; init; }
    public string? OwnHash { get; init; }
    public string? FullHash { get; init; }
    public long? DurationMS { get; init; }
    public long? CpuTimeMS { get; init; }
    public long? PeakMemoryBytes { get; init; }
    public bool Succeeded { get; init; }
    public int? ExitCode { get; init; }
    public int? TestsTotal { get; init; }
    public int? TestsPassed { get; init; }
    public int? TestsFailed { get; init; }
    public int? TestsSkipped { get; init; }
    public string? Sha { get; init; }
    public int? PullRequest { get; init; }
    public string? LogsPath { get; init; }
}
