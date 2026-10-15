using Bitenovac.RemoteBuildTool.Core.Processes;

namespace Bitenovac.RemoteBuildTool.Testing.Unit.Tests;

public sealed class TestSuiteTests
{
    [Fact]
    public void RunAll_ShouldReturnResultsInJobOrder_WhenJobsRunInParallel()
    {
        // Arrange
        var jobs = new List<TestJob>
        {
            new("A.Tests", () => TestFactory.Result(TestRunOutcome.Passed)),
            new("B.Tests", () => TestFactory.Result(TestRunOutcome.Failed)),
            new("C.Tests", () => TestFactory.Result(TestRunOutcome.NoTestsRan)),
        };

        // Act
        var results = TestSuite.RunAll(jobs, "results", maxParallelism: 3, (_, _) => { });

        // Assert
        Assert.Equal([TestRunOutcome.Passed, TestRunOutcome.Failed, TestRunOutcome.NoTestsRan], results.Select(result => result.Outcome));
    }

    [Fact]
    public void RunAll_ShouldFailOnlyThatJob_WhenAJobCannotStart()
    {
        // Arrange
        var jobs = new List<TestJob>
        {
            new("A.Tests", () => throw new InvalidOperationException("never built")),
            new("B.Tests", () => TestFactory.Result(TestRunOutcome.Passed)),
        };

        // Act
        var results = TestSuite.RunAll(jobs, "results", maxParallelism: 1, (_, _) => { });

        // Assert
        Assert.Equal(TestRunOutcome.Failed, results[0].Outcome);
        Assert.Equal(-1, results[0].ExitCode);
        Assert.Equal("results", results[0].ResultsDirectory);
        Assert.Equal("never built" + Environment.NewLine, results[0].Output);
        Assert.Equal(TestRunOutcome.Passed, results[1].Outcome);
    }

    [Fact]
    public void RunAll_ShouldHandOverEachJobWithItsResult_WhenJobsFinish()
    {
        // Arrange
        var jobs = new List<TestJob>
        {
            new("A.Tests", () => TestFactory.Result(TestRunOutcome.Passed, "first line")),
            new("B.Tests", () => TestFactory.Result(TestRunOutcome.Failed, "second line")),
        };
        var completed = new List<(string Name, string Output)>();

        // Act
        TestSuite.RunAll(jobs, "results", maxParallelism: 1, (job, result) => completed.Add((job.Name, result.Output)));

        // Assert
        Assert.Equal([("A.Tests", "first line"), ("B.Tests", "second line")], completed);
    }

    [Fact]
    public void RunAll_ShouldReturnNothing_WhenThereAreNoJobs()
    {
        // Arrange
        var jobs = new List<TestJob>();

        // Act
        var results = TestSuite.RunAll(jobs, "results", maxParallelism: 1, (_, _) => { });

        // Assert
        Assert.Empty(results);
    }
}
