using System.Text;
using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Planning;
using Bitenovac.RemoteBuildTool.Storage;
using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Commands;

/// <summary>
/// Provides the <c>audit</c> command, which checks the cache against a rebuild from scratch.
/// </summary>
/// <remarks>
/// <para>
/// Run after a cacheless <c>plan</c> and <c>build</c>: every project was rebuilt into this run's
/// own store, under the same fingerprint main would have looked it up by. Wherever main holds an
/// entry under that fingerprint, the two must hold the same output. A difference means the
/// fingerprint missed an input, so a pull request could have been handed stale output as a hit.
/// </para>
/// <para>
/// Only <c>bin/</c> is compared: it is what a cache hit hands on. <c>obj/</c> holds MSBuild's own
/// bookkeeping, which records when and where it ran. A project main holds under another
/// fingerprint, or not at all, cannot be compared and is skipped. Debug symbols record where the
/// checkout was, so both builds must run at the same path, as every CI job does at <c>/repo</c>.
/// </para>
/// </remarks>
internal static class AuditCommand
{
    private const string ComparedPrefix = "bin/";

    /// <summary>
    /// Compares every rebuilt project of a configuration with main's entry under the same fingerprint.
    /// </summary>
    /// <param name="options">The run's options.</param>
    /// <param name="configuration">The configuration to audit.</param>
    /// <param name="output">Where to report.</param>
    /// <returns>0 when every comparable project matches; otherwise, 1.</returns>
    /// <exception cref="InvalidOperationException">The plan was not cacheless, or a project was not rebuilt.</exception>
    public static int Run(PipelineOptions options, string configuration, PipelineOutput output)
    {
        using var stage = StageRecorder.Start(options, output, "audit", configuration);
        var entries = PlanState.Load(options.PlanFile).For(configuration);
        if (entries.Any(entry => !entry.Forced))
            throw new InvalidOperationException("audit compares a rebuild from scratch with the cache; plan and build with REMOTEBUILDTOOL_CACHELESS=true first.");

        var main = new LocalVolumeArtifactStore(options.MainStoreRoot);
        var rebuilt = new LocalVolumeArtifactStore(options.PrStoreRoot);
        var matched = 0;
        var skipped = 0;
        var mismatched = new List<(string Project, IReadOnlyList<string> Files)>();

        using (output.Section($"Comparing {entries.Count} rebuilt project(s) with main ({configuration})", collapse: true))
        {
            foreach (var entry in entries.OrderBy(entry => entry.ProjectPath, StringComparer.Ordinal))
            {
                var project = new ProjectId(entry.ProjectPath);
                if (!main.TryGetFileDigests(project, configuration, out var cachedHash, out var cachedFiles) || cachedHash.FullHash != entry.FullHash)
                {
                    skipped++;
                    output.WriteLine($"  skip {entry.ProjectPath}: main holds {(cachedHash == default ? "no entry" : "it under another fingerprint")}");
                    stage.AddTarget(Target(entry, "skipped", succeeded: true));
                    continue;
                }

                if (!rebuilt.TryGetFileDigests(project, configuration, out _, out var rebuiltFiles))
                    throw new InvalidOperationException($"{entry.ProjectPath} was not rebuilt; run 'build {configuration}' before 'audit {configuration}'.");

                var differing = Differences(cachedFiles, rebuiltFiles);
                if (differing.Count == 0)
                {
                    matched++;
                    output.WriteLine($"  match {entry.ProjectPath}");
                    stage.AddTarget(Target(entry, "match", succeeded: true));
                }
                else
                {
                    mismatched.Add((entry.ProjectPath, differing));
                    output.WriteLine($"  MISMATCH {entry.ProjectPath}: {string.Join(", ", differing)}");
                    stage.AddTarget(Target(entry, "mismatch", succeeded: false));
                }
            }
        }

        foreach (var (project, files) in mismatched)
            output.WriteFailure($"{project}: main holds different output under the same fingerprint ({string.Join(", ", files)}); its fingerprint misses an input.");

        stage.SetProjects(entries.Count, matched);
        output.WriteLine($"{configuration}: {matched} match, {mismatched.Count} mismatch, {skipped} not comparable.");
        output.AppendSummary(Summary(configuration, matched, skipped, mismatched));
        return stage.Complete(mismatched.Count == 0 ? 0 : 1);
    }

    /// <summary>
    /// Returns the compared files whose content differs between two entries, or that only one holds.
    /// </summary>
    /// <param name="cached">Main's entry: each file's digest by its path.</param>
    /// <param name="rebuilt">The rebuild's entry: each file's digest by its path.</param>
    /// <returns>The paths, ordered ordinally.</returns>
    public static IReadOnlyList<string> Differences(IReadOnlyDictionary<string, string> cached, IReadOnlyDictionary<string, string> rebuilt) =>
        [.. cached.Keys
            .Union(rebuilt.Keys)
            .Where(path => path.StartsWith(ComparedPrefix, StringComparison.Ordinal))
            .Where(path => cached.GetValueOrDefault(path) != rebuilt.GetValueOrDefault(path))
            .Order(StringComparer.Ordinal)];

    private static TargetInfo Target(PlanEntry entry, string outcome, bool succeeded) => new()
    {
        Project = entry.ProjectPath,
        Toolchain = entry.Toolchain,
        Kind = "audit",
        CacheOutcome = outcome,
        OwnHash = entry.OwnHash,
        FullHash = entry.FullHash,
        Succeeded = succeeded,
    };

    private static string Summary(string configuration, int matched, int skipped, List<(string Project, IReadOnlyList<string> Files)> mismatched)
    {
        var markdown = new StringBuilder()
            .AppendLine($"### {configuration} cache audit")
            .AppendLine()
            .AppendLine($"{matched} match, {mismatched.Count} mismatch, {skipped} not comparable.");

        if (mismatched.Count > 0)
        {
            markdown.AppendLine().AppendLine("| Project | Differing files |").AppendLine("|---|---|");
            foreach (var (project, files) in mismatched)
                markdown.AppendLine($"| `{project}` | {string.Join(", ", files.Select(file => $"`{file}`"))} |");
        }

        return markdown.AppendLine().ToString();
    }
}
