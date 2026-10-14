using System.Globalization;
using System.Text;
using Bitenovac.RemoteBuildTool.Testing;

namespace Bitenovac.RemoteBuildTool.Reporting;

/// <summary>
/// Represents the run summary of one configuration's tests: a row per test project.
/// </summary>
/// <param name="configuration">The configuration the tests ran in.</param>
internal sealed class TestSummary(string configuration)
{
    private readonly List<(string Name, string Result, string Time, bool Failed)> _rows = [];

    /// <summary>
    /// Records a test project whose result was reused from main.
    /// </summary>
    /// <param name="name">The test project's name.</param>
    public void Reused(string name) => _rows.Add((name, "reused", "", false));

    /// <summary>
    /// Records a test project that ran.
    /// </summary>
    /// <param name="name">The test project's name.</param>
    /// <param name="result">What the run ended in.</param>
    public void Ran(string name, TestRunResult result)
    {
        var outcome = result.Outcome switch
        {
            TestRunOutcome.Passed => "passed",
            TestRunOutcome.NoTestsRan => "ran no tests",
            _ => $"**failed** (exit {result.ExitCode})",
        };
        var time = result.Usage is { } usage
            ? string.Create(CultureInfo.InvariantCulture, $"{usage.Elapsed.TotalSeconds:0.0} s")
            : "";

        _rows.Add((name, outcome, time, result.Outcome == TestRunOutcome.Failed));
    }

    /// <summary>
    /// Returns the summary as a Markdown table, failures first.
    /// </summary>
    /// <returns>The Markdown.</returns>
    public string ToMarkdown()
    {
        var markdown = new StringBuilder()
            .AppendLine($"### {configuration} tests")
            .AppendLine()
            .AppendLine("| Test project | Result | Time |")
            .AppendLine("|---|---|---|");

        foreach (var (name, result, time, _) in _rows
            .OrderByDescending(row => row.Failed)
            .ThenBy(row => row.Name, StringComparer.Ordinal))
        {
            markdown.AppendLine($"| {name} | {result} | {time} |");
        }

        return markdown.AppendLine().ToString();
    }
}
