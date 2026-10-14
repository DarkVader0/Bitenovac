using System.Reflection;

namespace Bitenovac.RemoteBuildTool.Hashing;

/// <summary>
/// Provides the RemoteBuildTool's release version, which every fingerprint carries, so each release
/// starts from an empty cache rather than trusting what an older one stored.
/// </summary>
internal static class ToolVersion
{
    /// <summary>
    /// Gets the release version, as in <c>1.2.0</c>.
    /// </summary>
    public static string Value { get; } = Of(
        typeof(ToolVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        typeof(ToolVersion).Assembly.GetName().Version);

    /// <summary>
    /// Gets the fingerprint entry that ties every project to this release.
    /// </summary>
    public static string Entry => $"remotebuildtool:{Value}";

    /// <summary>
    /// Returns a release version from an assembly's versions, without the source revision a CI build
    /// appends to the informational one.
    /// </summary>
    /// <param name="informationalVersion">The informational version, as in <c>1.2.0+3f2a…</c>, if any.</param>
    /// <param name="assemblyVersion">The assembly version, used when there is no informational one.</param>
    /// <returns>The version, as in <c>1.2.0</c>.</returns>
    public static string Of(string? informationalVersion, Version? assemblyVersion) =>
        (informationalVersion ?? assemblyVersion?.ToString() ?? "0.0.0").Split('+')[0];
}
