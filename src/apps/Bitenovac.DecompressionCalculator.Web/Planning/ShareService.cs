using Microsoft.JSInterop;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Specifies how a share through the device's share sheet ended.
/// </summary>
public enum ShareOutcome
{
    Shared,
    Cancelled,
    Failed
}

/// <summary>
/// Shares and downloads the plan's PDF, and prints the plan, through the browser, using the app's own share script.
/// </summary>
public sealed class ShareService(IJSRuntime js) : IAsyncDisposable
{
    public const string PdfType = "application/pdf";

    private IJSObjectReference? _module;

    public async ValueTask<bool> CanShareFilesAsync() => await (await ModuleAsync()).InvokeAsync<bool>("canShareFiles");

    public async ValueTask<ShareOutcome> ShareFileAsync(byte[] bytes, string fileName, string type, string title, string text) =>
        Outcome(await (await ModuleAsync()).InvokeAsync<string>("shareFile", bytes, fileName, type, title, text));

    public async ValueTask DownloadAsync(byte[] bytes, string fileName, string type) =>
        await (await ModuleAsync()).InvokeVoidAsync("download", bytes, fileName, type);

    public async ValueTask PrintAsync() => await (await ModuleAsync()).InvokeVoidAsync("print");

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }

    private async ValueTask<IJSObjectReference> ModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/share.js");

    private static ShareOutcome Outcome(string result) => result switch
    {
        "shared" => ShareOutcome.Shared,
        "cancelled" => ShareOutcome.Cancelled,
        _ => ShareOutcome.Failed
    };
}
