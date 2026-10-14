using System.Diagnostics;
using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Planning;
using Bitenovac.RemoteBuildTool.Storage;
using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Commands;

/// <summary>
/// Provides the step that ends the service's daily rebuild: main switched to what the rebuild
/// built from scratch, every other entry it held deleted.
/// </summary>
/// <remarks>
/// <para>
/// Before an entry is replaced it is compared with the rebuild, <c>bin/</c> file for file, when main
/// held it under the same fingerprint; each project's outcome is a <see cref="CacheComparisonInfo"/>.
/// <c>obj/</c> is not compared: it holds MSBuild's own bookkeeping of when and where it ran.
/// </para>
/// <para>
/// Main changes only once every project of the plan is in the rebuild's store, so a rebuild that
/// fell short leaves it as it was.
/// </para>
/// </remarks>
internal static class SwitchCommand
{
    private const string ComparedPrefix = "bin/";

    /// <summary>
    /// Switches main to the rebuild in the run's own store.
    /// </summary>
    /// <param name="options">The rebuild's options: its own store, main, and its telemetry share.</param>
    /// <param name="output">Where to report.</param>
    /// <returns>0.</returns>
    /// <exception cref="InvalidOperationException">The plan was not cacheless, planned nothing, or a project was not rebuilt.</exception>
    public static int Run(PipelineOptions options, PipelineOutput output)
    {
        using var stage = StageRecorder.Start(options, output, "switch");
        var plan = PlanState.Load(options.PlanFile);
        var entries = PipelineOptions.Configurations
            .SelectMany(configuration => plan.For(configuration).Select(entry => (Configuration: configuration, Entry: entry)))
            .OrderBy(target => target.Configuration, StringComparer.Ordinal)
            .ThenBy(target => target.Entry.ProjectPath, StringComparer.Ordinal)
            .ToList();

        if (entries.Count == 0)
            throw new InvalidOperationException("the rebuild planned nothing; main is left as it was.");
        if (entries.Any(target => !target.Entry.Forced))
            throw new InvalidOperationException("main is switched only to a rebuild from scratch; plan with REMOTEBUILDTOOL_CACHELESS=true.");

        var main = new LocalVolumeArtifactStore(options.MainStoreRoot);
        var rebuilt = new LocalVolumeArtifactStore(options.PrStoreRoot);
        foreach (var (configuration, entry) in entries)
        {
            if (!rebuilt.TryGetHash(new ProjectId(entry.ProjectPath), configuration, out var hash) || hash.FullHash != entry.FullHash)
                throw new InvalidOperationException($"{configuration}/{entry.ProjectPath} was not rebuilt; main is left as it was.");
        }

        var identity = options.Identity ?? BuildIdentity.FromEnvironment(_ => null);
        var comparisons = new List<CacheComparisonInfo>();
        var started = Stopwatch.GetTimestamp();

        using (output.Section($"Switching main to the rebuild of {entries.Count} project(s)", collapse: true))
        {
            foreach (var (configuration, entry) in entries)
            {
                var project = new ProjectId(entry.ProjectPath);
                var comparison = Compare(main, rebuilt, project, configuration, entry, identity);
                comparisons.Add(comparison);
                main.Promote(project, configuration, rebuilt);
                output.WriteLine(comparison.DifferingFiles.Count == 0
                    ? $"  {comparison.Outcome} {configuration}/{entry.ProjectPath}"
                    : $"  {comparison.Outcome} {configuration}/{entry.ProjectPath}: {string.Join(", ", comparison.DifferingFiles)}");
            }

            var removed = main.Retain(entries.Select(target => (new ProjectId(target.Entry.ProjectPath), target.Configuration)));
            var blobs = main.Prune();
            output.WriteLine($"Deleted {removed} stale entr{(removed == 1 ? "y" : "ies")} and {blobs} unreferenced blob(s).");
        }

        stage.Add(StagePhase.Store, Stopwatch.GetElapsedTime(started));
        if (options.TelemetryRoot is { } root)
        {
            TelemetryWriter.Write(root, TelemetryWriter.CacheComparisonTable, $"{identity.BuildId}.switch.{identity.Machine}",
                DateTimeOffset.UtcNow, comparisons, TelemetryJsonContext.Default.CacheComparisonInfo);
        }

        int Count(string outcome) => comparisons.Count(comparison => comparison.Outcome == outcome);
        output.WriteLine($"Main switched: {Count("match")} match, {Count("mismatch")} mismatch, {Count("changed")} changed, {Count("new")} new.");
        stage.SetProjects(entries.Count, Count("match"));
        return stage.Complete(0);
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

    private static CacheComparisonInfo Compare(
        LocalVolumeArtifactStore main, LocalVolumeArtifactStore rebuilt, ProjectId project, string configuration, PlanEntry entry, BuildIdentity identity)
    {
        var held = main.TryGetFileDigests(project, configuration, out var cached, out var cachedFiles);
        rebuilt.TryGetFileDigests(project, configuration, out _, out var rebuiltFiles);
        var differing = held && cached.FullHash == entry.FullHash ? Differences(cachedFiles, rebuiltFiles) : [];

        return new CacheComparisonInfo
        {
            BuildId = identity.BuildId,
            Queue = identity.Queue,
            Pool = identity.Pool,
            Machine = identity.Machine,
            Sha = identity.Sha,
            Configuration = configuration,
            Project = entry.ProjectPath,
            Toolchain = entry.Toolchain,
            OwnHash = entry.OwnHash,
            FullHash = entry.FullHash,
            CachedFullHash = held ? cached.FullHash : null,
            Outcome = (held, cached.FullHash == entry.FullHash, differing.Count) switch
            {
                (false, _, _) => "new",
                (true, false, _) => "changed",
                (true, true, 0) => "match",
                _ => "mismatch",
            },
            DifferingFiles = differing,
            Time = DateTimeOffset.UtcNow,
            LogsPath = identity.LogsPath,
        };
    }
}
