namespace Bitenovac.RemoteBuildTool.Telemetry;

/// <summary>
/// Provides the names of the telemetry tables RemoteBuildTool writes.
/// </summary>
internal static class TelemetryTables
{
    public const string Build = "build";
    public const string Stage = "stage";
    public const string Target = "target";
    public const string Test = "test";
    public const string Machine = "machine";
    public const string CacheComparison = "cache_comparison";
}
