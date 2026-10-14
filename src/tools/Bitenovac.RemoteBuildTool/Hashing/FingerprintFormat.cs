namespace Bitenovac.RemoteBuildTool.Hashing;

/// <summary>
/// Provides the version of the rules a fingerprint is built by, which every fingerprint carries.
/// </summary>
/// <remarks>
/// A new release of the tool reuses what the old one cached, because only a project's inputs decide
/// whether it is rebuilt. Raise <see cref="Version"/> when a release changes which inputs are
/// hashed or how a project is built from them, so the cache cannot hand back what the old rules made.
/// </remarks>
internal static class FingerprintFormat
{
    /// <summary>
    /// The version of the fingerprint rules: 2 hashes what repository files contribute to a project.
    /// </summary>
    public const int Version = 2;

    /// <summary>
    /// Gets the fingerprint entry that ties every project to these rules.
    /// </summary>
    public static string Entry => $"fingerprint-format:{Version}";
}
