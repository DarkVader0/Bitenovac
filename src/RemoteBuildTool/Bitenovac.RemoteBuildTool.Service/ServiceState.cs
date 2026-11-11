namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Represents what the service has done, kept across restarts so it neither repeats nor skips work.
/// </summary>
internal sealed record ServiceState
{
    /// <summary>
    /// Gets or sets the merged pull requests the service has handled, promoted or not, newest last;
    /// only the recent ones are kept, which is all a later listing can repeat.
    /// </summary>
    public IReadOnlyList<int> PromotedPullRequests { get; set; } = [];

    /// <summary>
    /// Gets or sets the day, in UTC, the service last compacted telemetry and pruned logs.
    /// </summary>
    public DateOnly? HousekeptOn { get; set; }

    /// <summary>
    /// Gets or sets the day, in UTC, the service last rebuilt main.
    /// </summary>
    public DateOnly? RebuiltOn { get; set; }

    /// <summary>
    /// Gets or sets the builds the service has recorded from GitHub's completed runs, newest last; only
    /// the recent ones are kept, which is all a later listing can repeat.
    /// </summary>
    public IReadOnlyList<string> ReportedBuilds { get; set; } = [];

    /// <summary>
    /// Reads the state, or starts afresh when there is none.
    /// </summary>
    /// <param name="path">The state file.</param>
    /// <returns>The state.</returns>
    public static ServiceState Load(string path) => JsonFile.LoadOrNew(path, ServiceJsonContext.Default.ServiceState);

    /// <summary>
    /// Writes the state, whole or not at all.
    /// </summary>
    /// <param name="path">The state file.</param>
    public void Save(string path) => JsonFile.Save(path, this, ServiceJsonContext.Default.ServiceState);
}
