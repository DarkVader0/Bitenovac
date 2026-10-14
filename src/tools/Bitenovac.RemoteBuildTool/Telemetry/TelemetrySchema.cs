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
