using Microsoft.JSInterop;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Specifies how the app is coloured.
/// </summary>
public enum AppTheme
{
    /// <summary>
    /// Light surfaces and dark text; the default.
    /// </summary>
    Light,

    /// <summary>
    /// Dark surfaces and light text.
    /// </summary>
    Dark,

    /// <summary>
    /// Light or dark as the device is set, following it when it changes.
    /// </summary>
    System
}

/// <summary>
/// Keeps the theme chosen in this browser and applies it to the page.
/// </summary>
public sealed class ThemeService(IKeyValueStorage storage, IJSRuntime js)
{
    public const string Key = "bitenovac.theme";

    public async ValueTask<AppTheme> GetAsync() => await storage.GetAsync(Key) switch
    {
        "dark" => AppTheme.Dark,
        "system" => AppTheme.System,
        _ => AppTheme.Light
    };

    public ValueTask SetAsync(AppTheme theme) => js.InvokeVoidAsync("bitenovacTheme.set", theme.ToString().ToLowerInvariant());
}
