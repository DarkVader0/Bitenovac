using System.Text;
using Bitenovac.RemoteBuildTool.Planning;

namespace Bitenovac.RemoteBuildTool.Reporting;

/// <summary>
/// Provides a method for writing a plan as a run summary.
/// </summary>
internal static class PlanSummary
{
    /// <summary>
    /// Returns a Markdown table of each configuration's cache hits, followed by why each miss
    /// builds, folded away.
    /// </summary>
    /// <param name="byConfiguration">The plan's entries by configuration.</param>
    /// <returns>The Markdown.</returns>
    public static string ToMarkdown(IReadOnlyDictionary<string, List<PlanEntry>> byConfiguration)
    {
        var markdown = new StringBuilder()
            .AppendLine("### Plan")
            .AppendLine()
            .AppendLine("| Configuration | Projects | Cache hits | To build |")
            .AppendLine("|---|---|---|---|");

        foreach (var (configuration, entries) in byConfiguration)
        {
            var hits = entries.Count(entry => entry.Hit);
            markdown.AppendLine($"| {configuration} | {entries.Count} | {hits} | {entries.Count - hits} |");
        }

        markdown.AppendLine();

        foreach (var (configuration, entries) in byConfiguration)
        {
            var misses = entries
                .Where(entry => !entry.Hit)
                .OrderBy(entry => entry.ProjectPath, StringComparer.Ordinal)
                .ToList();
            if (misses.Count == 0)
                continue;

            markdown
                .AppendLine($"<details><summary>Why {misses.Count} {configuration} project(s) build</summary>")
                .AppendLine();
            foreach (var entry in misses)
                markdown.AppendLine($"- `{entry.ProjectPath}`: {entry.WhyCacheMiss}");
            markdown.AppendLine().AppendLine("</details>").AppendLine();
        }

        return markdown.ToString();
    }
}
