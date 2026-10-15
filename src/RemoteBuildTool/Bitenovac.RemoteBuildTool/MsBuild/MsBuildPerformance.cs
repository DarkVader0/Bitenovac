using System.Globalization;
using System.Text.RegularExpressions;

namespace Bitenovac.RemoteBuildTool.MsBuild;

/// <summary>
/// Provides a method for reading how long each project took from an MSBuild performance summary.
/// </summary>
internal static partial class MsBuildPerformance
{
    /// <summary>
    /// Reads the time each project took, from the "Project Performance Summary" of a file log
    /// written with <c>PerformanceSummary</c>.
    /// </summary>
    /// <param name="performanceLogPath">The log; an empty result when it does not exist.</param>
    /// <returns>
    /// The time by the project's full path, normalized by <see cref="Path.GetFullPath(string)"/> and
    /// compared without regard to case.
    /// </returns>
    /// <remarks>
    /// A project's time is what MSBuild spent in it over all its calls; in a graph build its
    /// references are built before it, so that time is its own.
    /// </remarks>
    public static IReadOnlyDictionary<string, TimeSpan> ProjectDurations(string performanceLogPath)
    {
        var durations = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(performanceLogPath))
            return durations;

        var inSummary = false;
        foreach (var line in File.ReadLines(performanceLogPath))
        {
            if (line.StartsWith("Project Performance Summary", StringComparison.Ordinal))
            {
                inSummary = true;
                continue;
            }

            if (!inSummary)
                continue;

            if (line.Length > 0 && !char.IsWhiteSpace(line[0]))
                break;

            // Projects are indented less than the targets listed under each.
            var match = ProjectLine().Match(line);
            if (match.Success && Path.IsPathRooted(match.Groups["project"].Value))
            {
                var project = Path.GetFullPath(match.Groups["project"].Value);
                var milliseconds = long.Parse(match.Groups["milliseconds"].Value, CultureInfo.InvariantCulture);
                durations[project] = durations.GetValueOrDefault(project) + TimeSpan.FromMilliseconds(milliseconds);
            }
        }

        return durations;
    }

    [GeneratedRegex(@"^\s{1,8}(?<milliseconds>\d+) ms\s+(?<project>\S.*?)\s+\d+ calls$")]
    private static partial Regex ProjectLine();
}
