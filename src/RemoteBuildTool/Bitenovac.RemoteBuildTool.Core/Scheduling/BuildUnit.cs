using Bitenovac.RemoteBuildTool.Core.Graph;

namespace Bitenovac.RemoteBuildTool.Core.Scheduling;

/// <summary>
/// Represents projects that are built together because they depend on one another, and on nothing
/// else that still has to be built.
/// </summary>
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
    /// <exception cref="ArgumentNullException"><paramref name="projects"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="projects"/> is empty.</exception>
    public BuildUnit(IEnumerable<ProjectId> projects, TimeSpan work, TimeSpan criticalPath)
    {
        ArgumentNullException.ThrowIfNull(projects);

        Projects = [.. projects];
        if (Projects.Count == 0)
            throw new ArgumentException("A build unit has at least one project.", nameof(projects));

        Work = work;
        CriticalPath = criticalPath;
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
}
