using System.Text.Json.Serialization.Metadata;

namespace Bitenovac.RemoteTools;

/// <summary>
/// Represents a service's state, shared by its pieces of work and saved on every change.
/// </summary>
/// <typeparam name="T">The state type.</typeparam>
/// <param name="path">The state file; what it holds is resumed from.</param>
/// <param name="typeInfo">The generated serialization metadata of <typeparamref name="T"/>.</param>
internal sealed class JsonStateStore<T>(string path, JsonTypeInfo<T> typeInfo)
    where T : new()
{
    private readonly Lock _gate = new();
    private T _state = JsonFile.LoadOrNew(path, typeInfo);

    /// <summary>
    /// Gets the state as it is now.
    /// </summary>
    public T Current
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
    public void Update(Func<T, T> change)
    {
        lock (_gate)
        {
            _state = change(_state);
            JsonFile.Save(path, _state, typeInfo);
        }
    }
}
