namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Represents the service's <see cref="ServiceState"/>, shared by its hosted services and saved on
/// every change.
/// </summary>
/// <param name="path">The state file; what it holds is resumed from.</param>
internal sealed class ServiceStateStore(string path)
{
    private readonly Lock _gate = new();
    private ServiceState _state = ServiceState.Load(path);

    /// <summary>
    /// Gets the state as it is now.
    /// </summary>
    public ServiceState Current
    {
        get
        {
            lock (_gate)
                return _state;
        }
    }

    /// <summary>
    /// Changes the state and saves it.
    /// </summary>
    /// <param name="change">Returns the new state from the current one.</param>
    public void Update(Func<ServiceState, ServiceState> change)
    {
        lock (_gate)
        {
            _state = change(_state);
            _state.Save(path);
        }
    }
}
