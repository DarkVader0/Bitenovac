using System.Text.Json.Serialization;

namespace Bitenovac.RemoteBuildTool.Telemetry;

/// <summary>
/// Provides the version of the telemetry contract every record carries.
/// </summary>
/// <remarks>
/// The JSON lines are the contract with whatever imports them, not the database. Raise the version
/// when a field changes meaning or goes away; adding a field does not need it.
/// </remarks>
internal static class TelemetrySchema
{
    /// <summary>
    /// The current version of the contract.
    /// </summary>
    public const int Version = 1;
}

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
}

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

/// <summary>
/// Represents one test case in one run of its test project.
/// </summary>
internal sealed record TestInfo
{
    public int SchemaVersion { get; init; } = TelemetrySchema.Version;
    public string BuildId { get; init; } = "";
    public string Queue { get; init; } = "";
    public string Pool { get; init; } = "";
    public string Machine { get; init; } = "";
    public string Stage { get; init; } = "";
    public string? Configuration { get; init; }
    public required string Project { get; init; }
    public required string TestName { get; init; }
    public required string Outcome { get; init; }
    public long? DurationMS { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Sha { get; init; }
    public int? PullRequest { get; init; }
}

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

/// <summary>
/// Provides the serialization metadata for the telemetry records, generated at compile time.
/// </summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(BuildInfo))]
[JsonSerializable(typeof(StageInfo))]
[JsonSerializable(typeof(TargetInfo))]
[JsonSerializable(typeof(TestInfo))]
[JsonSerializable(typeof(MachineSample))]
internal sealed partial class TelemetryJsonContext : JsonSerializerContext;
