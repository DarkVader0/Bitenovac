using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Keeps the diver's <see cref="UserPreferences"/> in the browser and tells the page when they change.
/// </summary>
public sealed class PreferencesStore(IKeyValueStorage storage, ThemeService theme)
{
    public const string Key = "bitenovac.preferences";

    private UserPreferences? _current;

    /// <summary>
    /// Occurs after the preferences are saved.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Returns the preferences, reading them once.
    /// </summary>
    /// <remarks>
    /// Before the preferences were a document of their own, only the theme was kept, under
    /// <see cref="ThemeService.Key"/>; that choice is carried over when there is no document yet.
    /// </remarks>
    public async ValueTask<UserPreferences> GetAsync() => _current ??= await ReadAsync();

    /// <summary>
    /// Saves the preferences, applies a changed theme, and raises <see cref="Changed"/>.
    /// </summary>
    public async ValueTask SaveAsync(UserPreferences preferences)
    {
        var before = _current?.Theme;
        _current = preferences;
        await storage.SetAsync(Key, UserPreferencesJson.Serialize(preferences));
        if (before != preferences.Theme)
        {
            await theme.ApplyAsync(preferences.Theme);
        }

        Changed?.Invoke();
    }

    private async ValueTask<UserPreferences> ReadAsync()
    {
        var legacy = ThemeService.Parse(await storage.GetAsync(ThemeService.Key));
        if (await storage.GetAsync(Key) is not { Length: > 0 } stored)
        {
            return UserPreferences.Default with { Theme = legacy };
        }

        var preferences = UserPreferencesJson.Deserialize(stored);
        if (preferences.Theme != legacy)
        {
            await theme.ApplyAsync(preferences.Theme);
        }

        return preferences;
    }
}
