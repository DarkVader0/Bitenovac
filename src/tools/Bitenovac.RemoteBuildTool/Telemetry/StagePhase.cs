namespace Bitenovac.RemoteBuildTool.Telemetry;

/// <summary>
/// Specifies a part of a stage whose time <see cref="StageInfo"/> reports on its own.
/// </summary>
internal enum StagePhase
{
    Evaluation,
    Restore,
    Hashing,
    CacheLookup,
    CacheRestore,
    Build,
    Test,
    Coverage,
    Publish,
    Pack,
    Store,
}
