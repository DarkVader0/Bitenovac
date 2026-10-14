using System.Globalization;
using System.Xml.Linq;

namespace Bitenovac.RemoteBuildTool.Testing;

/// <summary>
/// Represents one test case of a TRX report.
/// </summary>
/// <param name="Name">The test's name, as the report gives it.</param>
/// <param name="Outcome">The outcome, as in <c>Passed</c>, <c>Failed</c> or <c>NotExecuted</c>.</param>
/// <param name="Duration">How long the test took, if the report says.</param>
/// <param name="ErrorMessage">Why the test failed, if it did.</param>
internal sealed record TestCaseResult(string Name, string Outcome, TimeSpan? Duration, string? ErrorMessage);

/// <summary>
/// Provides a method for reading the test cases of a TRX report.
/// </summary>
internal static class TrxReader
{
    private const int MaximumErrorLength = 2000;

    private static readonly XNamespace TeamTest = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    /// <summary>
    /// Reads every test case result of a report.
    /// </summary>
    /// <param name="path">The report.</param>
    /// <returns>One result per test case, in report order.</returns>
    /// <remarks>An error message longer than 2 000 characters is cut short.</remarks>
    public static IReadOnlyList<TestCaseResult> Read(string path) =>
        [.. XDocument.Load(path)
            .Descendants(TeamTest + "UnitTestResult")
            .Select(result => new TestCaseResult(
                (string?)result.Attribute("testName") ?? "",
                (string?)result.Attribute("outcome") ?? "Unknown",
                TimeSpan.TryParse((string?)result.Attribute("duration"), CultureInfo.InvariantCulture, out var duration) ? duration : null,
                Shorten((string?)result.Element(TeamTest + "Output")?.Element(TeamTest + "ErrorInfo")?.Element(TeamTest + "Message"))))];

    private static string? Shorten(string? message) =>
        message is { Length: > MaximumErrorLength } ? message[..MaximumErrorLength] : message;
}
