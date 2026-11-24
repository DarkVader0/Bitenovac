using System.Globalization;
using System.Text.RegularExpressions;

namespace Bitenovac.RemoteTools;

/// <summary>
/// Provides methods for the names of release drops, <c>yyyy.MM.dd.NNN</c>: the date of the release
/// and a three-digit counter for the releases of that day.
/// </summary>
internal static partial class DropName
{
    /// <summary>
    /// Returns whether <paramref name="name"/> is a drop name.
    /// </summary>
    /// <param name="name">The name, as of a directory in the drop root.</param>
    /// <returns><see langword="true"/> for a name such as <c>2026.10.07.001</c>.</returns>
    public static bool IsDropName(string? name) => name is not null && Pattern().IsMatch(name);

    /// <summary>
    /// Returns the next unused drop name for the specified date.
    /// </summary>
    /// <param name="dropRoot">The directory that holds one subdirectory per release, named by its version.</param>
    /// <param name="date">The date of the release.</param>
    /// <returns>
    /// The date followed by a three-digit counter one higher than the highest already dropped on that
    /// date, starting at <c>001</c>; for example <c>2026.10.07.001</c>.
    /// </returns>
    public static string Next(string dropRoot, DateOnly date)
    {
        var prefix = date.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture) + ".";

        var highest = Directory.Exists(dropRoot)
            ? Directory.EnumerateDirectories(dropRoot)
                .Select(Path.GetFileName)
                .Where(name => name is not null && name.StartsWith(prefix, StringComparison.Ordinal))
                .Select(name => int.TryParse(name![prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var counter) ? counter : 0)
                .DefaultIfEmpty(0)
                .Max()
            : 0;

        return $"{prefix}{highest + 1:D3}";
    }

    /// <summary>
    /// Returns the newest drop in <paramref name="dropRoot"/>.
    /// </summary>
    /// <param name="dropRoot">The directory that holds one subdirectory per release, named by its version.</param>
    /// <returns>
    /// The name of the newest drop, or <see langword="null"/> when there is none. Anything not named
    /// as a drop is ignored, such as the <c>.staging-</c> directory of a release in progress.
    /// </returns>
    public static string? Newest(string dropRoot) =>
        Directory.Exists(dropRoot)
            ? Directory.EnumerateDirectories(dropRoot)
                .Select(Path.GetFileName)
                .Where(IsDropName)
                .Max(StringComparer.Ordinal)
            : null;

    [GeneratedRegex(@"^[0-9]{4}\.[0-9]{2}\.[0-9]{2}\.[0-9]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
