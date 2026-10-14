namespace Bitenovac.RemoteBuildTool.Telemetry;

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
