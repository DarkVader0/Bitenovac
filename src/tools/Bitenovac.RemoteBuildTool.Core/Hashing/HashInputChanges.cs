namespace Bitenovac.RemoteBuildTool.Core.Hashing;

/// <summary>
/// Represents how a project's own-hash inputs differ from an earlier set, each list holding input
/// keys ordered ordinally.
/// </summary>
/// <remarks>
/// An input's key is everything before its last <c>=</c>, for example <c>source:src/A.cs</c> of
/// <c>source:src/A.cs=&lt;content hash&gt;</c>. An input without <c>=</c> is its own key, so a
/// changed value of it shows as one key removed and another added.
/// </remarks>
/// <param name="Added">The keys only the current inputs have.</param>
/// <param name="Removed">The keys only the earlier inputs have.</param>
/// <param name="Changed">The keys both have, with different values.</param>
public sealed record HashInputChanges(IReadOnlyList<string> Added, IReadOnlyList<string> Removed, IReadOnlyList<string> Changed)
{
    /// <summary>
    /// Gets a value that indicates whether the two sets of inputs hold the same keys and values.
    /// </summary>
    public bool IsEmpty => Added.Count == 0 && Removed.Count == 0 && Changed.Count == 0;

    /// <summary>
    /// Compares two sets of own-hash inputs.
    /// </summary>
    /// <param name="previous">The inputs a stored entry was built from.</param>
    /// <param name="current">The inputs computed now.</param>
    /// <returns>The keys added, removed and changed between them.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="previous"/> or <paramref name="current"/> is <see langword="null"/>.
    /// </exception>
    public static HashInputChanges Compare(IEnumerable<string> previous, IEnumerable<string> current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        var before = previous.ToLookup(Key, StringComparer.Ordinal);
        var after = current.ToLookup(Key, StringComparer.Ordinal);

        var keys = before.Select(group => group.Key)
            .Union(after.Select(group => group.Key), StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        return new HashInputChanges(
            Added: [.. keys.Where(key => !before.Contains(key))],
            Removed: [.. keys.Where(key => !after.Contains(key))],
            Changed: [.. keys.Where(key => before.Contains(key) && after.Contains(key) && !before[key].ToHashSet(StringComparer.Ordinal).SetEquals(after[key]))]);
    }

    private static string Key(string input)
    {
        var separator = input.LastIndexOf('=');
        return separator < 0 ? input : input[..separator];
    }
}
