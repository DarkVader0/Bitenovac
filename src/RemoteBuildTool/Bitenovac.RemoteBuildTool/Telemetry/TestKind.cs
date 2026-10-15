namespace Bitenovac.RemoteBuildTool.Telemetry;

/// <summary>
/// Provides a method for naming the kind of tests a test project holds.
/// </summary>
internal static class TestKind
{
    /// <summary>
    /// Returns the test kind a test project's name implies, from the segment before <c>.Tests</c>.
    /// </summary>
    /// <param name="projectName">The test project's name, as in <c>Lib.Integration.Tests</c>.</param>
    /// <returns><c>Unit</c>, <c>Integration</c>, <c>E2E</c> or <c>Api</c>; otherwise <c>Other</c>.</returns>
    public static string Of(string projectName)
    {
        var segments = projectName.Split('.');
        return segments.Length >= 3 && segments[^1].Equals("Tests", StringComparison.OrdinalIgnoreCase)
            ? segments[^2] switch
            {
                var kind when kind.Equals("Unit", StringComparison.OrdinalIgnoreCase) => "Unit",
                var kind when kind.Equals("Integration", StringComparison.OrdinalIgnoreCase) => "Integration",
                var kind when kind.Equals("E2E", StringComparison.OrdinalIgnoreCase) => "E2E",
                var kind when kind.Equals("Api", StringComparison.OrdinalIgnoreCase) => "Api",
                _ => "Other",
            }
            : "Other";
    }

    /// <summary>
}
