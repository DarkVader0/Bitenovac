using System.Diagnostics;
using System.Globalization;
using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Core.Planning;
using Bitenovac.RemoteBuildTool.Planning;
using Bitenovac.RemoteBuildTool.Storage;
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
        var plan = PlanState.Load(options.PlanFile);
        var entries = plan.For(configuration);
        if (entries.Count == 0)
        {
            output.WriteLine("Nothing selected; skipping build.");
            return 0;
        }

        using var toolchains = ToolchainRegistry.Create(options.RepositoryRoot);
        var mainStore = new LocalVolumeArtifactStore(options.MainStoreRoot);
        var prStore = new LocalVolumeArtifactStore(options.PrStoreRoot);

        var stopwatch = Stopwatch.StartNew();
        (int Materialised, int Skipped, HashSet<PlanEntry> Unavailable) hits;
        using (output.Section($"Materialising cache hits from main ({configuration})"))
            hits = MaterialiseHits(options, entries, configuration, mainStore, toolchains);
        var (materialised, skipped, unavailable) = hits;
        output.WriteLine($"{configuration}: materialised {materialised} cache hit(s) from main"
            + (skipped > 0 ? $", {skipped} already present" : "") + ".");

        var toBuild = entries.Where(entry => !entry.Hit || unavailable.Contains(entry)).ToList();
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

            var exitCode = toolchains.For(name).Build(options, batch, configuration, output);
            if (exitCode != 0)
            {
                output.AppendSummary($"### {configuration} build\n\n**Build failed** after {Seconds(stopwatch.Elapsed)}; the errors are annotated above.\n");
                return exitCode;
            }
        }

        int staged;
        using (output.Section($"Staging rebuilt projects ({configuration})"))
            staged = StageMisses(entries, configuration, prStore, toolchains);

        foreach (var entry in entries)
            MaterialisedMarker.Write(options.RepositoryRoot, new ProjectId(entry.ProjectPath), configuration, entry.FullHash);

        output.WriteLine($"{configuration}: build complete, staged {staged} rebuilt project(s).");
        output.AppendSummary($"### {configuration} build\n\n{materialised + skipped} cache hit(s) reused, {toBuild.Count} project(s) rebuilt in {Seconds(stopwatch.Elapsed)}.\n");
        return 0;
    }

    private static string Seconds(TimeSpan elapsed) =>
        string.Create(CultureInfo.InvariantCulture, $"{elapsed.TotalSeconds:0.0} s");

    private static (int Materialised, int Skipped, HashSet<PlanEntry> Unavailable) MaterialiseHits(
        PipelineOptions options,
        IReadOnlyList<PlanEntry> entries,
        string configuration,
        LocalVolumeArtifactStore mainStore,
        ToolchainRegistry toolchains)
    {
        var materialised = 0;
        var skipped = 0;
        var unavailable = new HashSet<PlanEntry>();

        foreach (var entry in entries.Where(entry => entry.Hit))
        {
            var project = new ProjectId(entry.ProjectPath);
            var projectDirectory = Path.GetDirectoryName(entry.FullPath)!;
            var outputDirectories = toolchains.For(entry.Toolchain).OutputDirectories;

            if (MaterialisedMarker.Matches(options.RepositoryRoot, project, configuration, entry.FullHash, Path.Combine(projectDirectory, outputDirectories[0])))
            {
                skipped++;
                continue;
            }

            if (!mainStore.TryGet(project, configuration, projectDirectory, out _, [.. outputDirectories.Select(directory => directory + "/")]))
            {
                unavailable.Add(entry);
                continue;
            }

            foreach (var directory in outputDirectories)
                Touch(Path.Combine(projectDirectory, directory));
            materialised++;
        }

        return (materialised, skipped, unavailable);
    }

    /// <summary>
    /// Stages the output of every rebuilt project in this run's own store.
    /// </summary>
    /// <remarks>
    /// Cache hits are not staged, because later stages read them from <c>main</c>.
    /// </remarks>
    private static int StageMisses(IReadOnlyList<PlanEntry> entries, string configuration, LocalVolumeArtifactStore prStore, ToolchainRegistry toolchains)
    {
        var staged = 0;
        var inputsDirectory = Directory.CreateTempSubdirectory("bitenovac-remotebuildtool-inputs-").FullName;

        try
        {
            foreach (var entry in entries.Where(entry => !entry.Hit))
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
