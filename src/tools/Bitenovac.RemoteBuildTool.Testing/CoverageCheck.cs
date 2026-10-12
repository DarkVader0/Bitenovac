using Bitenovac.RemoteBuildTool.Core.Coverage;
using Bitenovac.RemoteBuildTool.Core.Graph;

namespace Bitenovac.RemoteBuildTool.Testing;

/// <summary>
/// Represents where a coverage check reads the run's Cobertura reports and writes the merged one.
/// </summary>
/// <param name="RepositoryRoot">The repository root, where the <c>reportgenerator</c> local tool is restored.</param>
/// <param name="CoverageDirectory">The directory holding every Cobertura report of the run.</param>
/// <param name="ReportDirectory">The directory to write the merged report into.</param>
/// <param name="IncludeHtml">Whether to also write the HTML report.</param>
internal sealed record CoverageReportLocation(string RepositoryRoot, string CoverageDirectory, string ReportDirectory, bool IncludeHtml);

/// <summary>
/// Provides methods for merging a run's coverage reports and checking every gated project against
/// its coverage policy.
/// </summary>
internal static class CoverageCheck
{
    /// <summary>
    /// Merges the run's reports and evaluates each gated project.
    /// </summary>
    /// <param name="gated">The policy of every project under the gate.</param>
    /// <param name="coverageNames">The name each project's code is reported under.</param>
    /// <param name="location">Where the reports are.</param>
    /// <param name="output">The writer for progress and results.</param>
    /// <param name="error">The writer for anything that fails the check.</param>
    /// <returns>0 if every gated project meets its policy; otherwise, 1.</returns>
    public static int Run(
        IReadOnlyDictionary<ProjectId, CoveragePolicy> gated,
        IReadOnlyDictionary<ProjectId, string> coverageNames,
        CoverageReportLocation location,
        TextWriter output,
        TextWriter error)
    {
        if (gated.Count == 0)
        {
            output.WriteLine("No selected project is under the coverage gate; nothing to check.");
            return 0;
        }

        if (!Directory.EnumerateFiles(location.CoverageDirectory, "*cobertura*.xml").Any())
        {
            error.WriteLine($"error: no coverage reports under {location.CoverageDirectory}. Did 'test' run?");
            return 1;
        }

        output.WriteLine("==> Merging coverage reports");
        var merged = CoverageReportGenerator.Merge(location.RepositoryRoot, location.CoverageDirectory, location.ReportDirectory, location.IncludeHtml);
        var measuredByName = CoverageReportReader.Read(merged);

        var measured = gated.Keys
            .Where(project => measuredByName.ContainsKey(coverageNames[project]))
            .ToDictionary(project => project, project => measuredByName[coverageNames[project]]);

        var results = CoverageGate.Evaluate(gated, measured);
        var failures = results.Where(result => !result.Passed).ToList();

        foreach (var result in results)
            output.WriteLine(result.Passed ? $"  OK   {result.Project}: {result.Reason}" : $"  FAIL {result.Reason}");

        if (failures.Count > 0)
        {
            error.WriteLine($"Open {location.ReportDirectory}/index.html to see which lines are uncovered.");
            return 1;
        }

        output.WriteLine("Every gated project meets its coverage policy.");
        return 0;
    }
}
