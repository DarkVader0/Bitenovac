namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Lists the languages the app is translated into. A language needs its resource files, a satellite entry in the project, and a line here and in wwwroot/index.html.
/// </summary>
internal static class AppLanguages
{
    /// <summary>
    /// The choice that follows the device's language; stored as no language at all.
    /// </summary>
    public const string MatchDevice = "";

    private static readonly (string Tag, string Name)[] Languages = [("en", "English"), ("de", "Deutsch")];

    /// <summary>
    /// Gets the choices in the order they are offered: matching the device first, then each language.
    /// </summary>
    public static IReadOnlyList<string> Choices { get; } = [MatchDevice, .. Languages.Select(static language => language.Tag)];

    /// <summary>
    /// Returns the name of a choice, each language named in itself, so a diver can find their own in a list they cannot read.
    /// </summary>
    /// <param name="choice">The choice.</param>
    /// <returns>The name shown for the choice.</returns>
    public static string Name(string choice) => choice == MatchDevice
        ? WebText.MatchDevice
        : Languages.FirstOrDefault(language => language.Tag == choice).Name ?? choice;
}
