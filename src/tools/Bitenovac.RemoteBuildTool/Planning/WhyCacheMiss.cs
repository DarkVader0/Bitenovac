using Bitenovac.RemoteBuildTool.Core.Hashing;
using Bitenovac.RemoteBuildTool.Core.Planning;

namespace Bitenovac.RemoteBuildTool.Planning;

/// <summary>
/// Provides methods for describing in one line why a project is rebuilt.
/// </summary>
internal static class WhyCacheMiss
{
    /// <summary>
    /// The path, within a stored entry, of the own-hash inputs the entry was built from.
    /// </summary>
    public const string InputsPath = "remotebuildtool/own-hash-inputs.txt";

    private const int ListedPerKind = 10;

    /// <summary>
    /// Describes why a project is a miss.
    /// </summary>
    /// <param name="decision">The project's cache decision.</param>
    /// <param name="ownHashInputs">The inputs the project's own hash was computed from now.</param>
    /// <param name="storedInputs">
    /// The inputs <c>main</c>'s entry was built from, or <see langword="null"/> if it does not record them.
    /// </param>
    /// <returns>The reasons, separated by <c>"; "</c>, or <see langword="null"/> for a hit.</returns>
    public static string? Describe(TargetDecision decision, IReadOnlyList<string> ownHashInputs, IReadOnlyList<string>? storedInputs)
    {
        var reasons = decision.MissReasons;
        if (reasons == CacheMissReasons.None)
            return null;

        var parts = new List<string>();

        if (reasons.HasFlag(CacheMissReasons.Forced))
            parts.Add("forced: the run ignores the cache");

        if (reasons.HasFlag(CacheMissReasons.NotCached))
            parts.Add("not in main's cache");

        if (reasons.HasFlag(CacheMissReasons.OwnInputsChanged))
            parts.Add(OwnInputs(ownHashInputs, storedInputs));

        if (reasons.HasFlag(CacheMissReasons.DependenciesChanged))
            parts.Add($"references changed: {string.Join(", ", decision.ChangedDependencies)}");

        if (reasons.HasFlag(CacheMissReasons.GlobalInputsChanged))
            parts.Add("RemoteBuildTool changed");

        return string.Join("; ", parts);
    }

    /// <summary>
    /// Reads the inputs file of a stored entry back into one input per line.
    /// </summary>
    public static IReadOnlyList<string> ParseInputs(string content) =>
        content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Writes inputs in the form <see cref="ParseInputs"/> reads.
    /// </summary>
    public static string FormatInputs(IReadOnlyList<string> inputs) =>
        string.Join('\n', inputs) + "\n";

    private static string OwnInputs(IReadOnlyList<string> ownHashInputs, IReadOnlyList<string>? storedInputs)
    {
        if (storedInputs is null)
            return "own inputs changed (main's entry does not record which)";

        var changes = HashInputChanges.Compare(storedInputs, ownHashInputs);
        if (changes.IsEmpty)
            return "own inputs changed";

        var kinds = new List<string>();
        AddKind(kinds, "changed", changes.Changed);
        AddKind(kinds, "added", changes.Added);
        AddKind(kinds, "removed", changes.Removed);
        return $"own inputs {string.Join("; ", kinds)}";
    }

    private static void AddKind(List<string> kinds, string label, IReadOnlyList<string> keys)
    {
        if (keys.Count == 0)
            return;

        var listed = string.Join(", ", keys.Take(ListedPerKind));
        kinds.Add(keys.Count > ListedPerKind ? $"{label} {listed} and {keys.Count - ListedPerKind} more" : $"{label} {listed}");
    }
}
