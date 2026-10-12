namespace Bitenovac.RemoteBuildTool.Core.Planning;

/// <summary>
/// Specifies why a project is a <see cref="CacheOutcome.Miss"/>.
/// </summary>
[Flags]
public enum CacheMissReasons
{
    /// <summary>
    /// The project is a hit.
    /// </summary>
    None = 0,

    /// <summary>
    /// The project was forced to a miss regardless of its hash.
    /// </summary>
    Forced = 1,

    /// <summary>
    /// Nothing is stored for the project.
    /// </summary>
    NotCached = 2,

    /// <summary>
    /// The project's own hash differs from the stored one: its own inputs changed.
    /// </summary>
    OwnInputsChanged = 4,

    /// <summary>
    /// The full hash of at least one referenced project differs from the one stored for it.
    /// </summary>
    DependenciesChanged = 8,

    /// <summary>
    /// Neither the project's own inputs nor its references changed, so an input folded into every
    /// full hash did.
    /// </summary>
    GlobalInputsChanged = 16,
}
