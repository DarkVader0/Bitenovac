using Bitenovac.RemoteBuildTool.Core.Processes;

namespace Bitenovac.RemoteBuildTool.Testing;

/// <summary>
/// Provides methods for running one test project and collecting coverage.
/// </summary>
/// <remarks>
/// A Microsoft.Testing.Platform test project is compiled as an application that hosts the test
/// platform, so its assembly is run with <c>dotnet exec</c>. Its apphost is not used: the artifact
/// store keeps content, not file modes, so one materialised from it comes back without its execute
/// bit.
/// </remarks>
internal static class TestRunner
{
    /// <summary>
    /// Runs one test project with coverage and, when <paramref name="report"/> names one, a TRX
    /// report named after the project.
    /// </summary>
    /// <param name="assemblyPath">The test project's assembly.</param>
    /// <param name="projectName">The project's name, which prefixes its coverage and names its report.</param>
    /// <param name="resultsDirectory">Where coverage and the report go.</param>
    /// <param name="report">Which TRX report the project can write.</param>
    /// <returns>The result.</returns>
    public static TestRunResult Run(string assemblyPath, string projectName, string resultsDirectory, TestReport report = TestReport.None)
    {
        Directory.CreateDirectory(resultsDirectory);

        var (exitCode, output, usage) = ProcessRunner.RunCaptured("dotnet", ["exec", assemblyPath, .. Arguments(projectName, resultsDirectory, report)], Path.GetDirectoryName(assemblyPath)!);

        var outcome = exitCode switch
        {
            0 => TestRunOutcome.Passed,
            5 or 8 => TestRunOutcome.NoTestsRan,
            _ => TestRunOutcome.Failed,
        };

        return new TestRunResult(outcome, exitCode, resultsDirectory, output, usage, ReportPath(projectName, resultsDirectory, report));
    }

    /// <summary>
    /// Returns the arguments that run a test project with coverage and the report it can write.
    /// </summary>
    /// <param name="projectName">The project's name, which prefixes its coverage and names its report.</param>
    /// <param name="resultsDirectory">Where coverage and the report go.</param>
    /// <param name="report">Which TRX report the project can write.</param>
    /// <returns>The arguments.</returns>
    public static IReadOnlyList<string> Arguments(string projectName, string resultsDirectory, TestReport report)
    {
        List<string> arguments =
        [
            "--coverlet",
            "--coverlet-output-format", "cobertura",
            "--coverlet-file-prefix", projectName,
            "--results-directory", resultsDirectory,
        ];

        switch (report)
        {
            case TestReport.Trx:
                arguments.AddRange("--report-trx", "--report-trx-filename", ReportFile(projectName));
                break;
            case TestReport.XunitTrx:
                arguments.AddRange("--report-xunit-trx", "--report-xunit-trx-filename", ReportFile(projectName));
                break;
        }

        return arguments;
    }

    /// <summary>
    /// Returns the report a run wrote, if it was asked for one and wrote it.
    /// </summary>
    /// <param name="projectName">The project's name, which names its report.</param>
    /// <param name="resultsDirectory">Where the report goes.</param>
    /// <param name="report">Which TRX report the project can write.</param>
    /// <returns>The report's path, or <see langword="null"/>.</returns>
    public static string? ReportPath(string projectName, string resultsDirectory, TestReport report)
    {
        var path = Path.Combine(resultsDirectory, ReportFile(projectName));
        return report != TestReport.None && File.Exists(path) ? path : null;
    }

    private static string ReportFile(string projectName) => $"{projectName}.trx";
}
