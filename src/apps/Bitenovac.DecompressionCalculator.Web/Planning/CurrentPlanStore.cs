using System.Text.Json;
using Bitenovac.DecompressionCalculator.Model;
using Microsoft.JSInterop;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Keeps the plan being edited in the browser's local storage, so it survives a reload.
/// </summary>
public sealed class CurrentPlanStore(IJSRuntime js)
{
    private const string Key = "bitenovac.current-plan";

    public async Task<DivePlanDocument?> LoadAsync()
    {
        var json = await js.InvokeAsync<string?>("localStorage.getItem", Key);
        if (json is null)
        {
            return null;
        }

        try
        {
            return DivePlanJson.Deserialize(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public ValueTask SaveAsync(DivePlanDocument plan) =>
        js.InvokeVoidAsync("localStorage.setItem", Key, DivePlanJson.Serialize(plan));
}
