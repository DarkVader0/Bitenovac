using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class WorkflowJobTests
{
    [Fact]
    public void Parse_ShouldReadEachJobsTimes_WhenTheResponseListsJobs()
    {
        // Arrange
        const string json = """
            {
              "total_count": 3,
              "jobs": [
                { "name": "Plan", "created_at": "2026-10-14T10:00:00Z", "started_at": "2026-10-14T10:00:05Z", "completed_at": "2026-10-14T10:00:50Z" },
                { "name": "PR", "created_at": "2026-10-14T10:03:00Z", "started_at": null, "completed_at": null },
                { "name": "Broken" }
              ]
            }
            """;

        // Act
        var jobs = WorkflowJob.Parse(json);

        // Assert
        Assert.Equal(
            [
                new WorkflowJob("Plan", new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 14, 10, 0, 5, TimeSpan.Zero), new DateTimeOffset(2026, 10, 14, 10, 0, 50, TimeSpan.Zero)),
                new WorkflowJob("PR", new DateTimeOffset(2026, 10, 14, 10, 3, 0, TimeSpan.Zero), null, null),
            ],
            jobs);
    }

    [Fact]
    public void Parse_ShouldReturnNothing_WhenTheResponseHasNoJobs()
    {
        // Arrange
        const string json = """{ "message": "Not Found" }""";

        // Act
        var jobs = WorkflowJob.Parse(json);

        // Assert
        Assert.Empty(jobs);
    }
}
