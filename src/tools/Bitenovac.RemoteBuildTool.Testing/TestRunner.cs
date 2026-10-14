using Bitenovac.RemoteBuildTool.Core.Processes;

namespace Bitenovac.RemoteBuildTool.Testing;

/// <summary>
/// Provides methods for running one test project by starting its own executable and collecting
/// coverage.
/// </summary>
/// <remarks>
/// A Microsoft.Testing.Platform test project is compiled as an executable that hosts the test
/// platform, so the apphost that MSBuild reports as <c>RunCommand</c> is started directly.
/// </remarks>
internal static class TestRunner
{
    private const UnixFileMode ExecuteBits = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    /// <summary>
    /// Runs one test project with coverage and, when <paramref name="report"/> names one, a TRX
    /// report named after the project.
    /// </summary>
    /// <param name="executablePath">The test project's executable.</param>
    /// <param name="projectName">The project's name, which prefixes its coverage and names its report.</param>
    /// <param name="resultsDirectory">Where coverage and the report go.</param>
    /// <param name="report">Which TRX report the project can write.</param>
    /// <returns>The result.</returns>
    public static TestRunResult Run(string executablePath, string projectName, string resultsDirectory, TestReport report = TestReport.None)
    {
        Directory.CreateDirectory(resultsDirectory);
        EnsureExecutable(executablePath);

        var (exitCode, output, usage) = ProcessRunner.RunCaptured(executablePath, Arguments(projectName, resultsDirectory, report), Path.GetDirectoryName(executablePath)!);

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

    // The artifact store keeps content, not file modes, so an apphost materialised from it comes
    // back without its execute bit.
    private static void EnsureExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        var mode = File.GetUnixFileMode(path);
        if ((mode & ExecuteBits) != ExecuteBits)
            File.SetUnixFileMode(path, mode | ExecuteBits);
    }
}
