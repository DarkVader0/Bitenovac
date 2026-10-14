using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class BuildTimelineTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FromJobs_ShouldKnowNothing_WhenTheRunHasNoJobs()
    {
        // Arrange
        WorkflowJob[] jobs = [];

        // Act
        var timeline = BuildTimeline.FromJobs(jobs, Created);

        // Assert
        Assert.Equal(BuildTimeline.Unknown, timeline);
    }

    [Fact]
    public void FromJobs_ShouldLeaveTheStartOut_WhenNoJobStarted()
    {
        // Arrange
        WorkflowJob[] jobs = [new("Plan", Created, null, null), new("PR", Created.AddMinutes(1), null, null)];

        // Act
        var timeline = BuildTimeline.FromJobs(jobs, Created.AddMinutes(5));

        // Assert
        Assert.Equal(new BuildTimeline(Created, null, 0, 0, 2), timeline);
    }
}
