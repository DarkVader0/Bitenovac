using Microsoft.Extensions.Logging;

namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Represents one piece of the RemoteBuildTool service's work.
/// </summary>
/// <param name="time">The clock the wait between turns runs on.</param>
/// <param name="logger">Where the work reports.</param>
internal abstract class RemoteBuildToolWork(TimeProvider time, ILogger logger) : ServiceWork(time, logger)
{
    /// <summary>
    /// Returns whether DuckDB failed, as when the telemetry database cannot be opened.
    /// </summary>
    /// <param name="exception">What a turn threw.</param>
    /// <returns><see langword="true"/> for a DuckDB failure.</returns>
    protected sealed override bool IsRecoverable(Exception exception) => exception is DuckDB.NET.Data.DuckDBException;
}
