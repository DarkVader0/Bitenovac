namespace Bitenovac.RemoteBuildTool.Telemetry;

/// <summary>
/// Represents how a project's clean rebuild compared with the entry main held for it when the
/// rebuild replaced it.
/// </summary>
/// <remarks>
/// <c>Outcome</c> is <c>match</c> or <c>mismatch</c> when main held the project under the same
/// fingerprint: a mismatch means the fingerprint misses an input that changes the output. Otherwise
/// it is <c>changed</c>, main holding it under another fingerprint, or <c>new</c>, main not holding it.
/// </remarks>
internal sealed record CacheComparisonInfo
{
    public int SchemaVersion { get; init; } = TelemetrySchema.Version;
    public required string BuildId { get; init; }
    public required string Queue { get; init; }
    public required string Pool { get; init; }
    public required string Machine { get; init; }
    public string? Sha { get; init; }
    public required string Configuration { get; init; }
    public required string Project { get; init; }
    public required string Toolchain { get; init; }
    public required string OwnHash { get; init; }
    public required string FullHash { get; init; }
    public string? CachedFullHash { get; init; }
    public required string Outcome { get; init; }
    public IReadOnlyList<string> DifferingFiles { get; init; } = [];
    public required DateTimeOffset Time { get; init; }
    public string? LogsPath { get; init; }
}
