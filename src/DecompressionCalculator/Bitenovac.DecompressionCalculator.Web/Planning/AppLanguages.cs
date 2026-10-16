using System.Globalization;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Lists the languages the app is translated into; adding one is a resource file and a line here.
/// </summary>
internal static class AppLanguages
{
    private static readonly (string Tag, string Name)[] Languages = [("en", "English")];

    public static IReadOnlyList<string> Tags { get; } = [.. Languages.Select(static language => language.Tag)];

    /// <remarks>Each language is named in itself, so a diver can find their own in a list they cannot read.</remarks>
    public static string Name(string tag) => Languages.FirstOrDefault(language => language.Tag == tag).Name ?? tag;

    public static CultureInfo Culture(string tag) =>
        CultureInfo.GetCultureInfo(Tags.Contains(tag) ? tag : UserPreferences.DefaultLanguage);
}
