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
/// Shares, downloads, and prints the plan's PDF through the browser, using the app's own share script.
/// </summary>
public sealed class ShareService(IJSRuntime js) : IAsyncDisposable
{
    public const string PdfType = "application/pdf";

    private IJSObjectReference? _module;

    public async ValueTask<bool> CanShareFilesAsync() => await (await ModuleAsync()).InvokeGuardedAsync<bool>("canShareFiles");

    public async ValueTask<ShareOutcome> ShareFileAsync(byte[] bytes, string fileName, string type, string title, string text) =>
        Outcome(await (await ModuleAsync()).InvokeGuardedAsync<string>("shareFile", bytes, fileName, type, title, text));

    public async ValueTask DownloadAsync(byte[] bytes, string fileName, string type) =>
        await (await ModuleAsync()).InvokeVoidGuardedAsync("download", bytes, fileName, type);

    /// <summary>
    /// Prints a PDF; where the browser cannot print one from the page, as on phones and tablets, it opens the PDF to print from instead.
    /// </summary>
    /// <returns><see langword="true"/> when the print dialog opened, <see langword="false"/> when the PDF was opened instead.</returns>
    public async ValueTask<bool> PrintPdfAsync(byte[] bytes) =>
        await (await ModuleAsync()).InvokeGuardedAsync<string>("printPdf", bytes) == "printing";

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }

    private async ValueTask<IJSObjectReference?> ModuleAsync() =>
        _module ??= await js.InvokeGuardedAsync<IJSObjectReference>("import", "./js/share.js");

    private static ShareOutcome Outcome(string? result) => result switch
    {
        "shared" => ShareOutcome.Shared,
        "cancelled" => ShareOutcome.Cancelled,
        _ => ShareOutcome.Failed
    };
}
