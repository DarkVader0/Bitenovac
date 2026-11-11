using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Core.Scheduling;
using Bitenovac.RemoteBuildTool.Storage;

namespace Bitenovac.RemoteBuildTool.Planning;

/// <summary>
/// Provides methods for working out which of the projects a stage builds are independent of each
/// other, and how many of those units it builds at once.
/// </summary>
internal static class BuildSharding
{
    // With no build time known at all every project counts the same, and a unit's critical path
    // is the length of its longest chain.
    private static readonly TimeSpan UnknownDuration = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Splits <paramref name="toBuild"/> into units and schedules them within <paramref name="limits"/>.
    /// </summary>
    /// <param name="toBuild">The projects to build.</param>
    /// <param name="limits">How many units may be built at once.</param>
    /// <param name="cores">The cores the stage has.</param>
    /// <param name="durations">
    /// How long each project took the last time it was built, where known. A project that has
    /// never been built is expected to take the average of those that have.
    /// </param>
    /// <returns>The schedule, the unit to start first leading.</returns>
    public static BuildSchedule Schedule(
        IReadOnlyList<PlanEntry> toBuild, SlotLimits limits, int cores, IReadOnlyDictionary<ProjectId, TimeSpan>? durations = null)
    {
        durations ??= new Dictionary<ProjectId, TimeSpan>();
        var expected = durations.Count == 0
            ? UnknownDuration
            : TimeSpan.FromTicks((long)durations.Values.Average(duration => duration.Ticks));

        var projects = toBuild.Select(entry => new ProjectId(entry.ProjectPath)).ToHashSet();
        var edges = toBuild.SelectMany(entry => entry.References
            .Select(reference => new ProjectId(reference))
            .Where(projects.Contains)
            .Select(reference => new ProjectEdge(new ProjectId(entry.ProjectPath), reference)));

        var units = BuildUnitPlanner.Plan(new ProjectGraph(projects, edges), projects, durations, expected);
        return BuildScheduler.Best(units, limits.Minimum, limits.Maximum, cores);
    }

    /// <summary>
    /// Reads how long each of <paramref name="toBuild"/> took the last time it was built into main.
    /// </summary>
    /// <param name="toBuild">The projects to build.</param>
    /// <param name="mainStore">Main.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The build time of each project main holds one for.</returns>
    public static IReadOnlyDictionary<ProjectId, TimeSpan> Durations(IReadOnlyList<PlanEntry> toBuild, LocalVolumeArtifactStore mainStore, string configuration)
    {
        var durations = new Dictionary<ProjectId, TimeSpan>();
        foreach (var project in toBuild.Select(entry => new ProjectId(entry.ProjectPath)))
        {
            if (BuildDuration.Read(mainStore, project, configuration) is { } duration)
                durations[project] = duration;
        }

        return durations;
    }

    /// <summary>
    /// Describes each unit of a schedule on a line of its own, for the log.
    /// </summary>
    /// <param name="schedule">The schedule.</param>
    /// <returns>The lines.</returns>
    public static string Describe(BuildSchedule schedule) =>
        string.Concat(schedule.Units.Select((unit, index) =>
            $"  unit {index + 1} (slot {unit.Slot + 1}): {unit.Unit.Projects.Count} project(s), from {unit.Unit.Projects[0].Value}{Environment.NewLine}"));
}
