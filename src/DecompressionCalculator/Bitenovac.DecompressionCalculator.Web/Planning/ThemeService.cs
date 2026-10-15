using Bitenovac.DecompressionCalculator.Model;
using Microsoft.JSInterop;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Applies the theme to the page, and keeps the copy of it that the page reads before it is drawn.
/// </summary>
/// <remarks>
/// The choice itself is part of the <see cref="UserPreferences"/>; the copy under <see cref="Key"/> lets
/// <c>theme.js</c> colour the first paint without waiting for the app to start.
/// </remarks>
public sealed class ThemeService(IJSRuntime js)
{
    public const string Key = "bitenovac.theme";

    public static ThemePreference Parse(string? copy) => copy switch
    {
        "dark" => ThemePreference.Dark,
        "system" => ThemePreference.System,
        _ => ThemePreference.Light
    };

    public static string Name(ThemePreference theme) => theme.ToString().ToLowerInvariant();

    public ValueTask ApplyAsync(ThemePreference theme) => js.InvokeVoidAsync("bitenovacTheme.set", Name(theme));
}
