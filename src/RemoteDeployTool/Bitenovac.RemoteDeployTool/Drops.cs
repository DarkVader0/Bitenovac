namespace Bitenovac.RemoteDeployTool;

/// <summary>
/// Provides methods for finding drops in the drop root.
/// </summary>
internal static class Drops
{
    /// <summary>
    /// Returns the directory of a drop, the newest when no name is given.
    /// </summary>
    /// <param name="options">Where the drops are.</param>
    /// <param name="name">The drop's name, or <see langword="null"/> for the newest.</param>
    /// <returns>The drop's directory.</returns>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="name"/> is not a drop name, no such drop exists, or there are no drops at all.
    /// </exception>
    public static string Resolve(DeployOptions options, string? name)
    {
        if (name is null)
        {
            return DropName.Newest(options.DropRoot) is { } newest
                ? Path.Combine(options.DropRoot, newest)
                : throw new InvalidOperationException($"there are no drops in {options.DropRoot}.");
        }

        if (!DropName.IsDropName(name))
            throw new InvalidOperationException($"'{name}' is not a drop name. Drops are named yyyy.MM.dd.NNN.");

        var directory = Path.Combine(options.DropRoot, name);
        return Directory.Exists(directory)
            ? directory
            : throw new InvalidOperationException($"there is no drop {name} in {options.DropRoot}.");
    }

    /// <summary>
    /// Returns the names of every drop, newest first.
    /// </summary>
    /// <param name="options">Where the drops are.</param>
    /// <returns>The names; none when the drop root does not exist.</returns>
    public static IReadOnlyList<string> All(DeployOptions options) =>
        Directory.Exists(options.DropRoot)
            ? [.. Directory.EnumerateDirectories(options.DropRoot)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(DropName.IsDropName)
                .OrderDescending(StringComparer.Ordinal)]
            : [];
}
