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
    public static string Value { get; } = ProductVersion.Of(typeof(ToolVersion).Assembly);

    /// <summary>
    /// Gets the fingerprint entry that ties every project to this release.
    /// </summary>
    public static string Entry => $"remotebuildtool:{Value}";
}
