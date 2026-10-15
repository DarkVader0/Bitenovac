using System.Diagnostics;
using System.Globalization;
using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Core.Planning;
using Bitenovac.RemoteBuildTool.Planning;
using Bitenovac.RemoteBuildTool.Storage;
using Bitenovac.RemoteBuildTool.Telemetry;
using Bitenovac.RemoteBuildTool.Toolchains;

namespace Bitenovac.RemoteBuildTool.Commands;

/// <summary>
/// Provides the <c>build</c> pipeline command.
/// </summary>
/// <remarks>
/// <para>
/// The command materializes the output of every cache hit from <c>main</c>, then has each
/// toolchain build the misses and any hit that <c>main</c> could not supply. Toolchains run in
/// the order returned by <see cref="ToolchainOrder.Resolve"/>.
/// </para>
/// <para>
/// Materialized output files are given a current write time, so a toolchain's up-to-date check
/// skips a hit that a miss references.
/// </para>
/// </remarks>
internal static class BuildCommand
{
    public static int Run(PipelineOptions options, string configuration, PipelineOutput output)
    {
        using var toolchains = ToolchainRegistry.Create(options.RepositoryRoot);
        return Run(options, configuration, output, toolchains);
    }

    public static int Run(PipelineOptions options, string configuration, PipelineOutput output, ToolchainRegistry toolchains)
    {
        using var stage = StageRecorder.Start(options, output, "build", configuration);
        var plan = PlanState.Load(options.PlanFile);
        var entries = plan.For(configuration);
        if (entries.Count == 0)
        {
            output.WriteLine("Nothing selected; skipping build.");
            return stage.Complete(0);
        }

        var mainStore = new LocalVolumeArtifactStore(options.MainStoreRoot);
        var prStore = new LocalVolumeArtifactStore(options.PrStoreRoot);

        var stopwatch = Stopwatch.StartNew();
        (int Materialised, int Skipped, HashSet<PlanEntry> Unavailable) hits;
        using (output.Section($"Materialising cache hits from main ({configuration})"))
        using (stage.Measure(StagePhase.CacheRestore))
            hits = MaterialiseHits(options, entries, configuration, mainStore, toolchains, stage);
        var (materialised, skipped, unavailable) = hits;
        output.WriteLine($"{configuration}: materialised {materialised} cache hit(s) from main"
            + (skipped > 0 ? $", {skipped} already present" : "") + ".");

        var toBuild = entries.Where(entry => !entry.Hit || unavailable.Contains(entry)).ToList();
        stage.SetProjects(entries.Count, entries.Count - toBuild.Count);
        var reasons = toBuild
            .OrderBy(entry => entry.ProjectPath, StringComparer.Ordinal)
            .Select(entry => $"  rebuild {entry.ProjectPath}: {entry.WhyCacheMiss ?? "main's entry could not be materialised"}{Environment.NewLine}");
        output.WriteGroup($"{configuration}: {toBuild.Count} project(s) to rebuild", string.Concat(reasons));

        var order = ToolchainOrder.Resolve(entries);

        foreach (var name in order)
        {
            var batch = toBuild.Where(entry => entry.Toolchain == name).ToList();
            if (batch.Count == 0)
                continue;

            var exitCode = toolchains.For(name).Build(options, batch, configuration, output, stage);
            if (exitCode != 0)
            {
                output.AppendSummary($"### {configuration} build\n\n**Build failed** after {Seconds(stopwatch.Elapsed)}; the errors are annotated above.\n");
                return stage.Complete(exitCode);
            }
        }

        int staged;
        using (output.Section($"Staging rebuilt projects ({configuration})"))
        using (stage.Measure(StagePhase.Store))
            staged = StageRebuilt(toBuild, configuration, prStore, toolchains);

        foreach (var entry in entries)
            MaterialisedMarker.Write(options.RepositoryRoot, new ProjectId(entry.ProjectPath), configuration, entry.FullHash);

        output.WriteLine($"{configuration}: build complete, staged {staged} rebuilt project(s).");
        output.AppendSummary($"### {configuration} build\n\n{materialised + skipped} cache hit(s) reused, {toBuild.Count} project(s) rebuilt in {Seconds(stopwatch.Elapsed)}.\n");
        return stage.Complete(0);
    }

    private static string Seconds(TimeSpan elapsed) =>
        string.Create(CultureInfo.InvariantCulture, $"{elapsed.TotalSeconds:0.0} s");

    // A hit's time is the time to restore it from main; it was not built.
    private static (int Materialised, int Skipped, HashSet<PlanEntry> Unavailable) MaterialiseHits(
        PipelineOptions options,
        IReadOnlyList<PlanEntry> entries,
        string configuration,
        LocalVolumeArtifactStore mainStore,
        ToolchainRegistry toolchains,
        StageRecorder stage)
    {
        var materialised = 0;
        var skipped = 0;
        var unavailable = new HashSet<PlanEntry>();

        foreach (var entry in entries.Where(entry => entry.Hit))
        {
            var started = Stopwatch.GetTimestamp();
            var project = new ProjectId(entry.ProjectPath);
            var projectDirectory = Path.GetDirectoryName(entry.FullPath)!;
            var outputDirectories = toolchains.For(entry.Toolchain).OutputDirectories;

            if (MaterialisedMarker.Matches(options.RepositoryRoot, project, configuration, entry.FullHash, Path.Combine(projectDirectory, outputDirectories[0])))
            {
                skipped++;
            }
            else if (mainStore.TryGet(project, configuration, projectDirectory, out _, [.. outputDirectories.Select(directory => directory + "/")]))
            {
                foreach (var directory in outputDirectories)
                    Touch(Path.Combine(projectDirectory, directory));
                materialised++;
            }
            else
            {
                unavailable.Add(entry);
                continue;
            }

            stage.AddTarget(new TargetInfo
            {
                Project = entry.ProjectPath,
                Toolchain = entry.Toolchain,
                Kind = "build",
                CacheOutcome = "hit",
                OwnHash = entry.OwnHash,
                FullHash = entry.FullHash,
                DurationMS = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                Succeeded = true,
            });
        }

        return (materialised, skipped, unavailable);
    }

    /// <summary>
    /// Stages the output of every rebuilt project in this run's own store.
    /// </summary>
    /// <remarks>
    /// Cache hits are not staged, because later stages read them from <c>main</c>. A hit main lost
    /// after the plan is rebuilt, so it is staged like a miss, and later stages and promotion take
    /// it from here.
    /// </remarks>
    private static int StageRebuilt(IReadOnlyList<PlanEntry> entries, string configuration, LocalVolumeArtifactStore prStore, ToolchainRegistry toolchains)
    {
        var staged = 0;
        var inputsDirectory = Directory.CreateTempSubdirectory("bitenovac-remotebuildtool-inputs-").FullName;

        try
        {
            foreach (var entry in entries)
            {
                var projectDirectory = Path.GetDirectoryName(entry.FullPath)!;
                var inputsFile = Path.Combine(inputsDirectory, $"{staged}.txt");
                File.WriteAllText(inputsFile, WhyCacheMiss.FormatInputs(entry.OwnHashInputs));

                var files = toolchains.For(entry.Toolchain).OutputDirectories
                    .SelectMany(directory => LocalVolumeArtifactStore.EnumerateAsSources(Path.Combine(projectDirectory, directory), directory + "/"))
                    .Append((WhyCacheMiss.InputsPath, inputsFile));

                prStore.Put(
                    new ProjectId(entry.ProjectPath),
                    configuration,
                    new StoredTargetHash(entry.OwnHash, entry.FullHash),
                    files);
                staged++;
            }
        }
        finally
        {
            Directory.Delete(inputsDirectory, recursive: true);
        }

        return staged;
    }

    private static void Touch(string directory)
    {
        if (!Directory.Exists(directory))
            return;

        var now = DateTime.UtcNow;
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            File.SetLastWriteTimeUtc(file, now);
    }
}
