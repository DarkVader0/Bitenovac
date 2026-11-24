using System.Reflection;

namespace Bitenovac.RemoteTools;

/// <summary>
/// Provides methods for reading the release version a tool was built as.
/// </summary>
internal static class ProductVersion
{
    /// <summary>
    /// Returns the release version of an assembly.
    /// </summary>
    /// <param name="assembly">The tool's assembly.</param>
    /// <returns>The version, as in <c>1.2.0</c>.</returns>
    public static string Of(Assembly assembly) =>
        Of(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion, assembly.GetName().Version);

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
