using Microsoft.Build.Locator;

namespace Bitenovac.RemoteBuildTool.MsBuild;

/// <summary>
/// Provides a method for pointing the process at the installed SDK's MSBuild assemblies.
/// </summary>
/// <remarks>
/// The tool ships without the MSBuild runtime assemblies, so registration must happen before the
/// JIT compiles any method that touches a <c>Microsoft.Build.*</c> type. Callers keep such methods
/// out of line for that reason.
/// </remarks>
internal static class MsBuildRegistration
{
    public static void EnsureRegistered()
    {
        if (!MSBuildLocator.IsRegistered)
            MSBuildLocator.RegisterDefaults();
    }
}
