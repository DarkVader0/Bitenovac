using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Core.Scheduling;

namespace Bitenovac.RemoteBuildTool.Planning;

/// <summary>
/// Provides methods for working out which of the projects a stage builds are independent of each
/// other, and how many of those units it builds at once.
/// </summary>
internal static class BuildSharding
{
    // Until builds record how long each project takes, every project counts the same, and a
    // unit's critical path is the length of its longest chain.
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Splits <paramref name="toBuild"/> into units and schedules them within <paramref name="limits"/>.
    /// </summary>
    /// <param name="toBuild">The projects to build.</param>
    /// <param name="limits">How many units may be built at once.</param>
    /// <param name="cores">The cores the stage has.</param>
    /// <returns>The schedule, the unit to start first leading.</returns>
    public static BuildSchedule Schedule(IReadOnlyList<PlanEntry> toBuild, SlotLimits limits, int cores)
    {
        var projects = toBuild.Select(entry => new ProjectId(entry.ProjectPath)).ToHashSet();
        var edges = toBuild.SelectMany(entry => entry.References
            .Select(reference => new ProjectId(reference))
            .Where(projects.Contains)
            .Select(reference => new ProjectEdge(new ProjectId(entry.ProjectPath), reference)));

        var units = BuildUnitPlanner.Plan(new ProjectGraph(projects, edges), projects, new Dictionary<ProjectId, TimeSpan>(), DefaultDuration);
        return BuildScheduler.Best(units, limits.Minimum, limits.Maximum, cores);
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
