namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Specifies a build the service runs itself, on a commit of the branch main follows.
/// </summary>
/// <param name="Kind">Names the build's logs folder and id, as in <c>Rebuild</c>.</param>
/// <param name="Queue">The queue its telemetry records, as in <c>Rebuild</c>.</param>
/// <param name="Cacheless">Whether to ignore main and build every project.</param>
/// <param name="Steps">The RemoteBuildTool commands to run in order; the first that fails ends the build.</param>
/// <param name="Finish">
/// What the service itself does once every step succeeded, with the build's options and its log;
/// its exit code is the build's.
/// </param>
internal sealed record ServiceBuildKind(
    string Kind,
    string Queue,
    bool Cacheless,
    IReadOnlyList<IReadOnlyList<string>> Steps,
    Func<PipelineOptions, PipelineOutput, CancellationToken, Task<int>>? Finish = null)
{
    /// <summary>
    /// Gets how long each step may run before it is stopped and the build ends as timed out; no
    /// limit unless set.
    /// </summary>
    public TimeSpan StepTimeout { get; init; } = Timeout.InfiniteTimeSpan;

    /// <summary>
    /// Builds and tests both configurations of a commit from scratch, then switches main to it.
    /// </summary>
    /// <param name="switchMain">Switches main to the rebuild; see <see cref="MainStoreWriter.SwitchAsync"/>.</param>
    /// <param name="stepTimeout">How long each step may run.</param>
    /// <returns>The kind of build.</returns>
    public static ServiceBuildKind Rebuild(Func<PipelineOptions, PipelineOutput, CancellationToken, Task<int>> switchMain, TimeSpan stepTimeout) => new("Rebuild", "Rebuild", Cacheless: true,
    [
        ["plan"],
        ["build", "Debug"],
        ["test", "Debug"],
        ["build", "Release"],
        ["test", "Release"],
    ],
    switchMain)
    {
        StepTimeout = stepTimeout,
    };
}
