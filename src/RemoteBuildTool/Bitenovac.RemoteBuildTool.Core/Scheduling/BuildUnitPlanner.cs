using Bitenovac.RemoteBuildTool.Core.Graph;

namespace Bitenovac.RemoteBuildTool.Core.Scheduling;

/// <summary>
/// Provides methods for splitting the projects a build has to build into units that can be built
/// independently of each other, and for ordering those units.
/// </summary>
public static class BuildUnitPlanner
{
    /// <summary>
    /// Splits the projects to build into units and orders them, the one to start first leading.
    /// </summary>
    /// <param name="graph">The project graph for one configuration.</param>
    /// <param name="toBuild">The projects to build, the plan's misses.</param>
    /// <param name="durations">How long each project is expected to take to build, where known.</param>
    /// <param name="defaultDuration">How long a project with no entry in <paramref name="durations"/> is expected to take.</param>
    /// <returns>
    /// <para>
    /// One unit for each group of projects in <paramref name="toBuild"/> connected by references
    /// among themselves. A reference to a project that is not built connects nothing: that project
    /// is restored, and whoever needs it finds it there.
    /// </para>
    /// <para>
    /// The units are ordered by <see cref="BuildUnit.CriticalPath"/>, longest first, since the
    /// longest chain bounds the whole build however many units run at once; then by
    /// <see cref="BuildUnit.Work"/>, largest first; then by path, so the order is the same on every run.
    /// </para>
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="graph"/>, <paramref name="toBuild"/>, or <paramref name="durations"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="toBuild"/> names a project that is not in <paramref name="graph"/>, or a duration is negative.
    /// </exception>
    /// <exception cref="ProjectGraphCycleException">The references in <paramref name="graph"/> form a cycle.</exception>
    public static IReadOnlyList<BuildUnit> Plan(
        ProjectGraph graph,
        IReadOnlySet<ProjectId> toBuild,
        IReadOnlyDictionary<ProjectId, TimeSpan> durations,
        TimeSpan defaultDuration)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(toBuild);
        ArgumentNullException.ThrowIfNull(durations);

        var known = graph.Projects.ToHashSet();
        if (toBuild.FirstOrDefault(project => !known.Contains(project)) is { } unknown)
            throw new ArgumentException($"A project to build is not in the graph: '{unknown}'.", nameof(toBuild));

        // Dependencies first, so a project's chain is known by the time something references it.
        var order = graph.GetBuildOrder().Where(toBuild.Contains).ToList();

        var leader = order.ToDictionary(project => project, project => project);
        var chain = new Dictionary<ProjectId, TimeSpan>();
        var duration = new Dictionary<ProjectId, TimeSpan>();
        foreach (var project in order)
        {
            duration[project] = durations.GetValueOrDefault(project, defaultDuration);
            if (duration[project] < TimeSpan.Zero)
                throw new ArgumentException($"The duration of '{project}' is negative.", nameof(durations));

            var longestBelow = TimeSpan.Zero;
            foreach (var dependency in graph.GetDependencies(project).Where(toBuild.Contains))
            {
                leader[Leader(project)] = Leader(dependency);
                if (chain[dependency] > longestBelow)
                    longestBelow = chain[dependency];
            }

            chain[project] = duration[project] + longestBelow;
        }

        return order
            .GroupBy(Leader)
            .Select(unit => new BuildUnit(
                unit,
                unit.Aggregate(TimeSpan.Zero, (work, project) => work + duration[project]),
                unit.Max(project => chain[project])))
            .OrderByDescending(unit => unit.CriticalPath)
            .ThenByDescending(unit => unit.Work)
            .ThenBy(unit => unit.Projects.Select(project => project.Value).Min(StringComparer.Ordinal), StringComparer.Ordinal)
            .ToList();

        // The projects of a unit share a leader; joining two units makes one leader follow the other.
        ProjectId Leader(ProjectId project)
        {
            while (leader[project] != project)
                project = leader[project];

            return project;
        }
    }
}
