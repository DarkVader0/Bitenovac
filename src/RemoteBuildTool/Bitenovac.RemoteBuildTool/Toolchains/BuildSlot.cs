namespace Bitenovac.RemoteBuildTool.Toolchains;

/// <summary>
/// Represents the share of a stage that one unit is built in, when the stage builds several units
/// at once.
/// </summary>
/// <param name="Name">What tells this unit's files and logs from the others', such as <c>unit-3</c>.</param>
/// <param name="Cores">How many cores the unit builds with.</param>
internal sealed record BuildSlot(string Name, int Cores);
