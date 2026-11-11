using Microsoft.JSInterop;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Stores strings in the browser's local storage.
/// </summary>
public sealed class BrowserStorage(IJSRuntime js) : IKeyValueStorage
{
    public ValueTask<string?> GetAsync(string key) => js.InvokeGuardedAsync<string?>("localStorage.getItem", key);

    public ValueTask SetAsync(string key, string value) => js.InvokeVoidGuardedAsync("localStorage.setItem", key, value);

    public ValueTask RemoveAsync(string key) => js.InvokeVoidGuardedAsync("localStorage.removeItem", key);
}
