namespace Bitenovac.RemoteDeployTool;

/// <summary>
/// Specifies how an artifact differs between two drops.
/// </summary>
internal enum ArtifactChange
{
    /// <summary>
    /// Only the newer drop holds it.
    /// </summary>
    Added,

    /// <summary>
    /// Both drops hold it, with different content.
    /// </summary>
    Changed,

    /// <summary>
    /// Both drops hold it, with the same content.
    /// </summary>
    Unchanged,

    /// <summary>
    /// Only the older drop holds it.
    /// </summary>
    Removed,
}

/// <summary>
/// Represents one artifact compared between two drops.
/// </summary>
/// <param name="Change">How it differs.</param>
/// <param name="Kind">Its kind, as in <see cref="DropArtifact.Kind"/>.</param>
/// <param name="Name">Its name, as in <see cref="DropArtifact.Name"/>.</param>
/// <param name="From">The artifact in the older drop, if it holds it.</param>
/// <param name="To">The artifact in the newer drop, if it holds it.</param>
internal sealed record ArtifactComparison(ArtifactChange Change, string Kind, string Name, DropArtifact? From, DropArtifact? To);

/// <summary>
/// Provides methods for telling what changed between two drops.
/// </summary>
internal static class DropChanges
{
    /// <summary>
    /// Compares the artifacts of two drops.
    /// </summary>
    /// <param name="from">The older drop's manifest.</param>
    /// <param name="to">The newer drop's manifest.</param>
    /// <returns>
    /// Every artifact either drop holds, matched by kind and name and ordered by name, then kind. An
    /// artifact both hold has changed exactly when its <see cref="DropArtifact.Sha256"/> differs; a
    /// package whose version moved has, as its file has.
    /// </returns>
    public static IReadOnlyList<ArtifactComparison> Compare(DropManifest from, DropManifest to)
    {
        var older = from.Artifacts.ToDictionary(artifact => (artifact.Kind, artifact.Name));
        var newer = to.Artifacts.ToDictionary(artifact => (artifact.Kind, artifact.Name));

        return [.. older.Keys.Union(newer.Keys)
            .OrderBy(key => key.Name, StringComparer.Ordinal)
            .ThenBy(key => key.Kind, StringComparer.Ordinal)
            .Select(key =>
            {
                var before = older.GetValueOrDefault(key);
                var after = newer.GetValueOrDefault(key);
                var change = (before, after) switch
                {
                    (null, _) => ArtifactChange.Added,
                    (_, null) => ArtifactChange.Removed,
                    _ when before.Sha256 == after.Sha256 => ArtifactChange.Unchanged,
                    _ => ArtifactChange.Changed,
                };
                return new ArtifactComparison(change, key.Kind, key.Name, before, after);
            })];
    }
}
