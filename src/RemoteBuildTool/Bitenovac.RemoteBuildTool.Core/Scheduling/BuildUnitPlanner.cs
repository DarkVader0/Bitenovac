using Bitenovac.RemoteBuildTool.Core.Graph;

namespace Bitenovac.RemoteBuildTool.Core.Scheduling;

/// <summary>
/// Provides methods for splitting the projects a build has to build into units that are each built
/// on their own, and for ordering those units.
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
    /// <param name="splitInto">
    /// How many units to split a group of connected projects into once what they share is built; one
    /// leaves every group whole.
    /// </param>
    /// <returns>
    /// <para>
    /// One unit for each group of projects in <paramref name="toBuild"/> connected by references
    /// among themselves. A reference to a project that is not built connects nothing: that project
    /// is restored, and whoever needs it finds it there.
    /// </para>
    /// <para>
    /// With <paramref name="splitInto"/> above one, a group that several projects sit on top of is
    /// split further. The projects more than one of those top projects needs become a unit of
    /// their own, built first. What is left belongs to one top project each, so those parts no
    /// longer depend on one another; they are spread over at most <paramref name="splitInto"/>
    /// units, each of which names the shared unit among its <see cref="BuildUnit.Dependencies"/>.
    /// </para>
    /// <para>
    /// The units are ordered by <see cref="BuildUnit.RemainingPath"/>, longest first, since the
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
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="splitInto"/> is less than one.</exception>
    /// <exception cref="ProjectGraphCycleException">The references in <paramref name="graph"/> form a cycle.</exception>
    public static IReadOnlyList<BuildUnit> Plan(
        ProjectGraph graph,
        IReadOnlySet<ProjectId> toBuild,
        IReadOnlyDictionary<ProjectId, TimeSpan> durations,
        TimeSpan defaultDuration,
        int splitInto = 1)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(toBuild);
        ArgumentNullException.ThrowIfNull(durations);
        ArgumentOutOfRangeException.ThrowIfLessThan(splitInto, 1);

        var known = graph.Projects.ToHashSet();
        if (toBuild.FirstOrDefault(project => !known.Contains(project)) is { } unknown)
            throw new ArgumentException($"A project to build is not in the graph: '{unknown}'.", nameof(toBuild));

        // Dependencies first, so whatever a project needs has been seen by the time it is.
        var order = graph.GetBuildOrder().Where(toBuild.Contains).ToList();

        var leader = order.ToDictionary(project => project, project => project);
        var duration = new Dictionary<ProjectId, TimeSpan>();
        foreach (var project in order)
        {
            duration[project] = durations.GetValueOrDefault(project, defaultDuration);
            if (duration[project] < TimeSpan.Zero)
                throw new ArgumentException($"The duration of '{project}' is negative.", nameof(durations));

            foreach (var dependency in Needs(project))
                leader[Leader(project)] = Leader(dependency);
        }

        return order
            .GroupBy(Leader)
            .SelectMany(group => Split([.. group]))
            .OrderByDescending(unit => unit.RemainingPath)
            .ThenByDescending(unit => unit.Work)
            .ThenBy(unit => unit.Projects.Select(project => project.Value).Min(StringComparer.Ordinal), StringComparer.Ordinal)
            .ToList();

        IEnumerable<ProjectId> Needs(ProjectId project) => graph.GetDependencies(project).Where(toBuild.Contains);

        // The projects of a group share a leader; joining two groups makes one leader follow the other.
        ProjectId Leader(ProjectId project)
        {
            while (leader[project] != project)
                project = leader[project];

            return project;
        }

        // A unit of the given projects, which are in build order, with its chains measured inside it.
        BuildUnit Unit(IReadOnlyList<ProjectId> projects, BuildUnit? after = null, TimeSpan waiting = default)
        {
            var inside = projects.ToHashSet();
            var chain = new Dictionary<ProjectId, TimeSpan>();
            foreach (var project in projects)
            {
                chain[project] = duration[project] + Needs(project)
                    .Where(inside.Contains)
                    .Select(dependency => chain[dependency])
                    .DefaultIfEmpty(TimeSpan.Zero)
                    .Max();
            }

            var criticalPath = chain.Values.Max();
            return new BuildUnit(
                projects,
                projects.Aggregate(TimeSpan.Zero, (work, project) => work + duration[project]),
                criticalPath,
                after is null ? [] : [after],
                criticalPath + waiting);
        }

        IEnumerable<BuildUnit> Split(IReadOnlyList<ProjectId> group)
        {
            // The top projects are those nothing else in the group needs. Going down from them,
            // a project belongs to the one top project that needs it, or to none when several do.
            var needed = group.SelectMany(Needs).ToHashSet();
            var owner = group.Where(project => !needed.Contains(project)).ToDictionary(top => top, top => (ProjectId?)top);
            if (splitInto == 1 || owner.Count == 1)
                return [Unit(group)];

            foreach (var project in group.Reverse())
            {
                foreach (var dependency in Needs(project))
                    owner[dependency] = owner.TryGetValue(dependency, out var other) && other != owner[project] ? null : owner[project];
            }

            // What each top project owns is spread over the units, the longest chain first and
            // always into the unit with the least work so far.
            var parts = group
                .Where(project => owner[project] is not null)
                .GroupBy(project => owner[project]!)
                .Select(part => Unit([.. part]))
                .OrderByDescending(part => part.CriticalPath)
                .ThenByDescending(part => part.Work)
                .ThenBy(part => part.Projects[^1].Value, StringComparer.Ordinal);

            var spread = Enumerable.Range(0, splitInto).Select(_ => new List<BuildUnit>()).ToList();
            foreach (var part in parts)
                spread.MinBy(unit => unit.Aggregate(TimeSpan.Zero, (work, piece) => work + piece.Work))!.Add(part);

            var position = group.Select((project, index) => (project, index)).ToDictionary(item => item.project, item => item.index);
            var tops = spread
                .Where(unit => unit.Count > 0)
                .Select(unit => unit.SelectMany(part => part.Projects).OrderBy(project => position[project]).ToList())
                .ToList();

            var shared = Unit([.. group.Where(project => owner[project] is null)], waiting: tops.Max(top => Unit(top).CriticalPath));
            return tops.Select(top => Unit(top, after: shared)).Prepend(shared);
        }
    }
}
