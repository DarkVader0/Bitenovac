using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Core.Hashing;

namespace Bitenovac.RemoteBuildTool.Core.Planning;

/// <summary>
/// Represents what to do with one project in one configuration: whether to rebuild and retest it,
/// and whether to hold it to its coverage policy.
/// </summary>
public sealed record TargetDecision
{
    /// <summary>
    /// Gets the project this decision applies to.
    /// </summary>
    public required ProjectId Project { get; init; }

    /// <summary>
    /// Gets the hashes computed for the project.
    /// </summary>
    public required TargetHash Computed { get; init; }

    /// <summary>
    /// Gets a value that indicates whether the project was forced to a miss regardless of its hash.
    /// </summary>
    public required bool Forced { get; init; }

    /// <summary>
    /// Gets the caching outcome for building and testing the project.
    /// </summary>
    /// <value>
    /// <see cref="CacheOutcome.Hit"/> if the stored artifact and test results can be reused;
    /// <see cref="CacheOutcome.Miss"/> if the project must be rebuilt and, for a test project, its
    /// tests run again.
    /// </value>
    public required CacheOutcome BuildOutcome { get; init; }

    /// <summary>
    /// Gets why the project is a miss.
    /// </summary>
    /// <value>
    /// <see cref="CacheMissReasons.None"/> for a hit. A forced project, or one with nothing stored,
    /// reports only that reason.
    /// </value>
    public required CacheMissReasons MissReasons { get; init; }

    /// <summary>
    /// Gets the referenced projects whose full hash differs from the one stored for them.
    /// </summary>
    /// <value>
    /// The changed references, ordered ordinally; empty unless <see cref="MissReasons"/> includes
    /// <see cref="CacheMissReasons.DependenciesChanged"/>.
    /// </value>
    public required IReadOnlyList<ProjectId> ChangedDependencies { get; init; }

    /// <summary>
    /// Gets a value that indicates whether the project must be held to its coverage policy.
    /// </summary>
    /// <value>
    /// <see langword="true"/> if the project was forced, nothing is stored for it, or its own hash
    /// differs from the stored one; otherwise, <see langword="false"/>.
    /// </value>
    public required bool ShouldGateCoverage { get; init; }
}
