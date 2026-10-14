using Bitenovac.RemoteBuildTool.Core.Hashing;
using Bitenovac.RemoteBuildTool.Planning;

namespace Bitenovac.RemoteBuildTool.Commands;

/// <summary>
/// Provides the <c>compare-graphs</c> command, which shows how the build graph changed between two
/// builds: projects that came or went, references that changed, and which inputs of a project's
/// fingerprint changed.
/// </summary>
internal static class CompareGraphsCommand
{
    /// <summary>
    /// Compares two build graphs, configuration by configuration.
    /// </summary>
    /// <param name="oldPath">The earlier build's <c>build-graph.json</c>.</param>
    /// <param name="newPath">The later build's.</param>
    /// <param name="output">Where to write the differences.</param>
    /// <returns>0.</returns>
    /// <exception cref="InvalidOperationException">A file is missing or is not a build graph.</exception>
    public static int Run(string oldPath, string newPath, PipelineOutput output)
    {
        var before = Read(oldPath);
        var after = Read(newPath);

        output.WriteLine($"{before.BuildId} ({before.Sha ?? "unknown commit"}) -> {after.BuildId} ({after.Sha ?? "unknown commit"})");
        if (before.FingerprintFormat != after.FingerprintFormat)
            output.WriteLine($"fingerprint rules changed ({before.FingerprintFormat} -> {after.FingerprintFormat}): every hash differs, inputs compare as usual");

        foreach (var configuration in before.Configurations.Keys.Union(after.Configurations.Keys).Order(StringComparer.Ordinal))
        {
            var old = Index(before, configuration);
            var current = Index(after, configuration);
            var lines = new List<string>();

            foreach (var project in current.Keys.Except(old.Keys).Order(StringComparer.Ordinal))
                lines.Add($"  + {project}");
            foreach (var project in old.Keys.Except(current.Keys).Order(StringComparer.Ordinal))
                lines.Add($"  - {project}");

            foreach (var project in old.Keys.Intersect(current.Keys).Order(StringComparer.Ordinal))
            {
                var (was, now) = (old[project], current[project]);
                var added = now.References.Except(was.References).Select(reference => $"+{reference}");
                var removed = was.References.Except(now.References).Select(reference => $"-{reference}");
                var references = added.Concat(removed).ToList();
                if (references.Count > 0)
                    lines.Add($"  ~ {project}: references {string.Join(", ", references)}");

                var inputs = HashInputChanges.Compare(was.OwnHashInputs, now.OwnHashInputs);
                if (!inputs.IsEmpty)
                {
                    var changes = new[] { ("changed", inputs.Changed), ("added", inputs.Added), ("removed", inputs.Removed) }
                        .Where(kind => kind.Item2.Count > 0)
                        .Select(kind => $"{kind.Item1} {string.Join(", ", kind.Item2)}");
                    lines.Add($"  ~ {project}: inputs {string.Join("; ", changes)}");
                }
            }

            output.WriteLine(lines.Count == 0 ? $"{configuration}: no change" : $"{configuration}:");
            foreach (var line in lines)
                output.WriteLine(line);
        }

        return 0;
    }

    private static BuildGraphFile Read(string path) =>
        File.Exists(path) ? BuildGraphFile.Read(path) : throw new InvalidOperationException($"No build graph at {path}.");

    private static Dictionary<string, BuildGraphNode> Index(BuildGraphFile graph, string configuration) =>
        graph.Configurations.TryGetValue(configuration, out var nodes)
            ? nodes.ToDictionary(node => node.Project, StringComparer.Ordinal)
            : [];
}
