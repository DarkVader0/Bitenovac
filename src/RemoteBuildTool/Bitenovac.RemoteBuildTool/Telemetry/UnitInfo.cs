namespace Bitenovac.RemoteBuildTool.Telemetry;

/// <summary>
/// Represents one unit of a stage that built several at once: where and when it was built, and how
/// its time compared with what was expected of it.
/// </summary>
internal sealed record UnitInfo
{
    /// <summary>
    /// Gets the version of the telemetry contract the record was written under.
    /// </summary>
    public int SchemaVersion { get; init; } = TelemetrySchema.Version;

    /// <summary>
    /// Gets the build the unit belongs to.
    /// </summary>
    public string BuildId { get; init; } = "";

    /// <summary>
    /// Gets the queue of the build.
    /// </summary>
    public string Queue { get; init; } = "";

    /// <summary>
    /// Gets the pool of the build.
    /// </summary>
    public string Pool { get; init; } = "";

    /// <summary>
    /// Gets the machine the unit was built on.
    /// </summary>
    public string Machine { get; init; } = "";

    /// <summary>
    /// Gets the stage that built the unit, as in <c>build-Debug</c>.
    /// </summary>
    public string Stage { get; init; } = "";

    /// <summary>
    /// Gets the configuration.
    /// </summary>
    public string? Configuration { get; init; }

    /// <summary>
    /// Gets the unit's place in the order the stage took its units in, from one; the unit expected
    /// to bound the stage is first.
    /// </summary>
    public required int Unit { get; init; }

    /// <summary>
    /// Gets the slot that built the unit, from one.
    /// </summary>
    public required int Slot { get; init; }

    /// <summary>
    /// Gets how many units the stage built at once.
    /// </summary>
    public required int Slots { get; init; }

    /// <summary>
    /// Gets how many cores each slot built with.
    /// </summary>
    public required int CoresPerSlot { get; init; }

    /// <summary>
    /// Gets how many projects the unit holds.
    /// </summary>
    public required int Projects { get; init; }

    /// <summary>
    /// Gets the unit's first project in build order, which names it.
    /// </summary>
    public required string FirstProject { get; init; }

    /// <summary>
    /// Gets the time building every project of the unit was expected to take, one after another.
    /// </summary>
    public required long ExpectedWorkMS { get; init; }

    /// <summary>
    /// Gets the time the unit's longest chain of projects was expected to take.
    /// </summary>
    public required long ExpectedCriticalPathMS { get; init; }

    /// <summary>
    /// Gets when the unit started.
    /// </summary>
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// Gets how long the unit took.
    /// </summary>
    public required long DurationMS { get; init; }

    /// <summary>
    /// Gets a value indicating whether the unit built.
    /// </summary>
    public required bool Succeeded { get; init; }

    /// <summary>
    /// Gets the exit code of the unit's build.
    /// </summary>
    public required int ExitCode { get; init; }

    /// <summary>
    /// Gets the commit the build ran on.
    /// </summary>
    public string? Sha { get; init; }

    /// <summary>
    /// Gets the pull request the build ran for, if any.
    /// </summary>
    public int? PullRequest { get; init; }

    /// <summary>
    /// Gets the build's folder in the logs share, where the unit's binary log is kept.
    /// </summary>
    public string? LogsPath { get; init; }
}
