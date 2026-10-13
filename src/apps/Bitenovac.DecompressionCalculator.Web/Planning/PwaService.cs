using Microsoft.JSInterop;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Represents what the browser offers for installing and updating the app.
/// </summary>
/// <param name="CanInstall">Whether the browser can install the app from a prompt.</param>
/// <param name="ManualInstall">
/// Where the app can only be installed by hand: <c>ios</c> from the share sheet, <c>mac</c> from Safari's
/// File menu; <see langword="null"/> otherwise.
/// </param>
/// <param name="UpdateReady">Whether a new version is downloaded and waits for a reload.</param>
/// <remarks>Neither way to install is offered once the app is installed, or for the rest of a visit once the offer was dismissed.</remarks>
public sealed record PwaState(bool CanInstall, string? ManualInstall, bool UpdateReady)
{
    public static PwaState None { get; } = new(false, null, false);
}

/// <summary>
/// Installs and updates the app through the browser, using the app's own service worker script.
/// </summary>
/// <remarks>
/// The script is loaded by <c>index.html</c> before Blazor starts, so an install prompt the browser
/// raises early is not lost; importing it here returns that same module.
/// </remarks>
public sealed class PwaService(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;
    private IJSObjectReference? _subscription;
    private DotNetObjectReference<PwaService>? _self;

    public event Action? Changed;

    public PwaState State { get; private set; } = PwaState.None;

    public async ValueTask StartAsync()
    {
        if (_subscription is not null)
        {
            return;
        }

        _self = DotNetObjectReference.Create(this);
        _subscription = await (await ModuleAsync()).InvokeAsync<IJSObjectReference>("subscribe", _self);
        await RefreshAsync();
    }

    public async ValueTask<bool> InstallAsync() => await (await ModuleAsync()).InvokeAsync<bool>("install");

    public async ValueTask DismissInstallAsync() => await (await ModuleAsync()).InvokeVoidAsync("dismissInstall");

    public async ValueTask ApplyUpdateAsync() => await (await ModuleAsync()).InvokeVoidAsync("applyUpdate");

    [JSInvokable]
    public async Task OnChanged() => await RefreshAsync();

    public async ValueTask DisposeAsync()
    {
        if (_subscription is not null)
        {
            await _subscription.InvokeVoidAsync("dispose");
            await _subscription.DisposeAsync();
        }

        if (_module is not null)
        {
            await _module.DisposeAsync();
        }

        _self?.Dispose();
    }

    private async ValueTask RefreshAsync()
    {
        State = await (await ModuleAsync()).InvokeAsync<PwaState>("state");
        Changed?.Invoke();
    }

    private async ValueTask<IJSObjectReference> ModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/pwa.js");
}
