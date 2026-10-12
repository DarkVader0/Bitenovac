using Microsoft.JSInterop;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Stores strings in the browser's local storage.
/// </summary>
public sealed class BrowserStorage(IJSRuntime js) : IKeyValueStorage
{
    public ValueTask<string?> GetAsync(string key) => js.InvokeAsync<string?>("localStorage.getItem", key);

    public ValueTask SetAsync(string key, string value) => js.InvokeVoidAsync("localStorage.setItem", key, value);

    public ValueTask RemoveAsync(string key) => js.InvokeVoidAsync("localStorage.removeItem", key);
}
