namespace Bitenovac.RemoteBuildTool.Core.Graph;

/// <summary>
/// Represents a project, identified by the path of its project file relative to the repository root.
/// </summary>
public sealed record ProjectId : IComparable<ProjectId>
{
    /// <summary>
    /// Gets the repository-relative path of the project file.
    /// </summary>
    /// <value>
    /// The path, using forward slashes.
    /// </value>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectId"/> class from a repository-relative path.
    /// </summary>
    /// <param name="value">The repository-relative path, using either slash style.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is empty or consists only of white-space characters.
    /// </exception>
    public ProjectId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Replace('\\', '/');
    }

    /// <inheritdoc/>
    public int CompareTo(ProjectId? other) =>
        string.CompareOrdinal(Value, other?.Value);

    /// <summary>
    /// Determines whether one project sorts before another, ordinally by path; a missing project sorts first.
    /// </summary>
    /// <param name="left">The first project.</param>
    /// <param name="right">The second project.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> sorts before <paramref name="right"/>.</returns>
    public static bool operator <(ProjectId? left, ProjectId? right) =>
        left is null ? right is not null : left.CompareTo(right) < 0;

    /// <summary>
    /// Determines whether one project sorts before or with another, ordinally by path; a missing project sorts first.
    /// </summary>
    /// <param name="left">The first project.</param>
    /// <param name="right">The second project.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> does not sort after <paramref name="right"/>.</returns>
    public static bool operator <=(ProjectId? left, ProjectId? right) =>
        left is null || left.CompareTo(right) <= 0;

    /// <summary>
    /// Determines whether one project sorts after another, ordinally by path; a missing project sorts first.
    /// </summary>
    /// <param name="left">The first project.</param>
    /// <param name="right">The second project.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> sorts after <paramref name="right"/>.</returns>
    public static bool operator >(ProjectId? left, ProjectId? right) =>
        left is not null && left.CompareTo(right) > 0;

    /// <summary>
    /// Determines whether one project sorts after or with another, ordinally by path; a missing project sorts first.
    /// </summary>
    /// <param name="left">The first project.</param>
    /// <param name="right">The second project.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> does not sort before <paramref name="right"/>.</returns>
    public static bool operator >=(ProjectId? left, ProjectId? right) =>
        left is null ? right is null : left.CompareTo(right) >= 0;

    /// <inheritdoc/>
    public override string ToString() => Value;
}
