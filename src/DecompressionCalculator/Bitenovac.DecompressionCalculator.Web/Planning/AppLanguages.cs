namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Lists the languages the app is translated into. A language needs its resource files, a satellite entry in the project, and a line here and in wwwroot/index.html.
/// </summary>
internal static class AppLanguages
{
    private static readonly (string Tag, string Name)[] Languages = [("en", "English"), ("de", "Deutsch")];

    public static IReadOnlyList<string> Tags { get; } = [.. Languages.Select(static language => language.Tag)];

    /// <remarks>Each language is named in itself, so a diver can find their own in a list they cannot read.</remarks>
    public static string Name(string tag) => Languages.FirstOrDefault(language => language.Tag == tag).Name ?? tag;
}
