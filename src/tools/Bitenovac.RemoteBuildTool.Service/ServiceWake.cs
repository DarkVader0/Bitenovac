namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Represents a request that the promotion work take its next turn now rather than when its wait
/// ends, as when a pull request was merged.
/// </summary>
/// <remarks>
/// Requests made while one is already pending count as one.
/// </remarks>
internal sealed class ServiceWake
{
    private readonly SemaphoreSlim _pending = new(0, 1);
    private readonly Lock _gate = new();

    /// <summary>
    /// Gets the file of the socket that <c>remotebuildtool-service wake</c> connects to.
    /// </summary>
    /// <param name="host">The machine's settings.</param>
    /// <returns>The socket's path, in the service's work directory.</returns>
    public static string SocketPath(HostSettings host) => Path.Combine(host.WorkRoot, "wake.sock");

    /// <summary>
    /// Asks for a turn now.
    /// </summary>
    public void Request()
    {
        lock (_gate)
        {
            if (_pending.CurrentCount == 0)
                _pending.Release();
        }
    }

    /// <summary>
    /// Waits until a turn is asked for.
    /// </summary>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>A task that completes once a turn is asked for.</returns>
    public Task WaitAsync(CancellationToken cancellationToken) => _pending.WaitAsync(cancellationToken);
}
