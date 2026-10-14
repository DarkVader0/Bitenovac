using Bitenovac.RemoteBuildTool.Core.Processes;
using Bitenovac.RemoteBuildTool.Reporting;
using Bitenovac.RemoteBuildTool.Testing;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class TestSummaryTests
{
    [Fact]
    public void ToMarkdown_ShouldListFailuresFirstThenByName_WhenProjectsRanAndWereReused()
    {
        // Arrange
        var summary = new TestSummary("Debug");
        var usage = new ResourceUsage(TimeSpan.FromSeconds(8.43), CpuTime: null, PeakMemoryBytes: null);
        summary.Ran("C.Tests", new TestRunResult(TestRunOutcome.Passed, 0, "results", "", usage));
        summary.Reused("A.Tests");
        summary.Ran("D.Tests", new TestRunResult(TestRunOutcome.Failed, 2, "results", ""));
        summary.Ran("B.Tests", new TestRunResult(TestRunOutcome.NoTestsRan, 8, "results", ""));

        // Act
        var markdown = summary.ToMarkdown();

        // Assert
        Assert.Equal("""
            ### Debug tests

            | Test project | Result | Time |
            |---|---|---|
            | D.Tests | **failed** (exit 2) |  |
            | A.Tests | reused |  |
            | B.Tests | ran no tests |  |
            | C.Tests | passed | 8.4 s |


            """.ReplaceLineEndings(Environment.NewLine), markdown);
    }
}
