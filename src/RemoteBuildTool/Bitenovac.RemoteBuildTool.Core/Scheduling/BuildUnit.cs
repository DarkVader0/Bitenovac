using Bitenovac.RemoteBuildTool.Core.Graph;

namespace Bitenovac.RemoteBuildTool.Core.Scheduling;

/// <summary>
/// Represents projects that are built together, in one build of their own.
/// </summary>
/// <remarks>
/// A unit depends on nothing that still has to be built, except the units it names as its
/// <see cref="Dependencies"/>, which are built before it.
/// </remarks>
public sealed class BuildUnit
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BuildUnit"/> class.
    /// </summary>
    /// <param name="projects">The unit's projects, each after the projects it references.</param>
    /// <param name="work">The time building every project is expected to take, one after another.</param>
    /// <param name="criticalPath">
    /// The time the unit is expected to take with unlimited parallelism: that of its longest chain of
    /// projects that each reference the one before.
    /// </param>
    /// <param name="dependencies">The units that are built before this one, or <see langword="null"/> for none.</param>
    /// <param name="remainingPath">
    /// The time from the start of this unit to the end of the last unit that waits for it, with
    /// unlimited parallelism; or <see langword="null"/> when nothing waits for it, which makes it
    /// <paramref name="criticalPath"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="projects"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="projects"/> is empty.</exception>
    public BuildUnit(
        IEnumerable<ProjectId> projects,
        TimeSpan work,
        TimeSpan criticalPath,
        IEnumerable<BuildUnit>? dependencies = null,
        TimeSpan? remainingPath = null)
    {
        ArgumentNullException.ThrowIfNull(projects);

        Projects = [.. projects];
        if (Projects.Count == 0)
            throw new ArgumentException("A build unit has at least one project.", nameof(projects));

        Work = work;
        CriticalPath = criticalPath;
        Dependencies = [.. dependencies ?? []];
        RemainingPath = remainingPath ?? criticalPath;
    }

    /// <summary>
    /// Gets the unit's projects, each after the projects it references.
    /// </summary>
    public IReadOnlyList<ProjectId> Projects { get; }

    /// <summary>
    /// Gets the time building every project is expected to take, one after another.
    /// </summary>
    public TimeSpan Work { get; }

    /// <summary>
    /// Gets the time the unit is expected to take with unlimited parallelism: that of its longest
    /// chain of projects that each reference the one before.
    /// </summary>
    public TimeSpan CriticalPath { get; }

    /// <summary>
    /// Gets the units that are built before this one.
    /// </summary>
    public IReadOnlyList<BuildUnit> Dependencies { get; }

    /// <summary>
    /// Gets the time from the start of this unit to the end of the last unit that waits for it, with
    /// unlimited parallelism. A unit nothing waits for has its <see cref="CriticalPath"/> left.
    /// </summary>
    public TimeSpan RemainingPath { get; }
}
