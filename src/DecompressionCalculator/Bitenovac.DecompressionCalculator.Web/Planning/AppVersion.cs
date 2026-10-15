using System.Reflection;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Provides the version of the running app: the Official drop it was built in, such as 2026.10.13.001.
/// </summary>
public static class AppVersion
{
    /// <remarks>The build appends the commit after a plus sign; the sheet shows only the version.</remarks>
    public static string Current { get; } =
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "unknown";
}
