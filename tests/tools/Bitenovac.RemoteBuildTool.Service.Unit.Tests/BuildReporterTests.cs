using System.Net;
using Bitenovac.RemoteBuildTool.Service;
using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Service.Unit.Tests;

public sealed class BuildReporterTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReportAsync_ShouldRecordEachNewRunThatHasStages_WhenGitHubListsCompletedRuns()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var host = Host(directory);
        WriteStage(host.TelemetryRoot, "7-1");
        var github = new FakeGitHub(
            runs: """
                { "workflow_runs": [
                    { "id": 7, "run_attempt": 1, "name": "PR", "conclusion": "failure", "head_sha": "abc", "head_branch": "feature",
                      "pull_requests": [ { "number": 62 } ], "updated_at": "2026-10-14T11:00:00Z" },
                    { "id": 8, "run_attempt": 1, "name": "Deploy web", "conclusion": "success", "updated_at": "2026-10-14T11:30:00Z" },
                    { "id": 6, "run_attempt": 1, "name": "PR", "conclusion": "success", "updated_at": "2026-10-14T10:00:00Z" }
                ] }
                """,
            jobs: """
                { "jobs": [
                    { "name": "Plan", "created_at": "2026-10-14T10:58:00Z", "started_at": "2026-10-14T10:58:10Z", "completed_at": "2026-10-14T10:59:00Z" }
                ] }
                """);
        using var http = GitHubClient.CreateHttpClient("https://api.test", "token", github);
        var reporter = new BuildReporter(new GitHubClient(http, "owner/repo"), host);

        // Act
        var reported = await reporter.ReportAsync(["6-1"], Now, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["6-1", "7-1", "8-1"], reported);
        var build = Assert.Single(TelemetryWriter.Read(host.TelemetryRoot, TelemetryWriter.BuildTable, "", TelemetryJsonContext.Default.BuildInfo));
        Assert.Equal("7-1", build.BuildId);
        Assert.Equal("PR", build.Queue);
        Assert.Equal("failure", build.Result);
        Assert.Equal("docker", build.Pool);
        Assert.Equal("owner/repo", build.Repository);
        Assert.Equal("feature", build.Ref);
        Assert.Equal("abc", build.Sha);
        Assert.Equal(62, build.PullRequest);
        Assert.Equal(10_000, build.QueueTimeMS);
        Assert.Equal(1, build.Jobs);
        Assert.Equal(new DateTimeOffset(2026, 10, 14, 11, 0, 0, TimeSpan.Zero), build.EndTime);
        Assert.Equal(["/repos/owner/repo/actions/runs/7/attempts/1/jobs"], github.JobRequests);
    }

    [Fact]
    public async Task ReportAsync_ShouldPreferWhatTheStagesRecorded_WhenTheyNameTheCommit()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var host = Host(directory);
        WriteStage(host.TelemetryRoot, "7-1", "refs/pull/62/merge", "merge", 62);
        var github = new FakeGitHub(
            runs: """{ "workflow_runs": [ { "id": 7, "run_attempt": 1, "name": "PR", "head_sha": "head", "head_branch": "feature", "updated_at": "2026-10-14T11:00:00Z" } ] }""",
            jobs: """{ "jobs": [] }""");
        using var http = GitHubClient.CreateHttpClient("https://api.test", "token", github);
        var reporter = new BuildReporter(new GitHubClient(http, "owner/repo"), host);

        // Act
        await reporter.ReportAsync([], Now, TestContext.Current.CancellationToken);

        // Assert
        var build = Assert.Single(TelemetryWriter.Read(host.TelemetryRoot, TelemetryWriter.BuildTable, "", TelemetryJsonContext.Default.BuildInfo));
        Assert.Equal(("refs/pull/62/merge", "merge", 62), (build.Ref, build.Sha, build.PullRequest));
    }

    [Fact]
    public async Task ReportAsync_ShouldKeepOnlyTheNewest_WhenItRemembersTooMany()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var github = new FakeGitHub(
            runs: """{ "workflow_runs": [ { "id": 9, "run_attempt": 1, "updated_at": "2026-10-14T11:00:00Z" } ] }""",
            jobs: """{ "jobs": [] }""");
        using var http = GitHubClient.CreateHttpClient("https://api.test", "token", github);
        var reporter = new BuildReporter(new GitHubClient(http, "owner/repo"), Host(directory));
        var remembered = Enumerable.Range(1, 5000).Select(id => $"old-{id}").ToList();

        // Act
        var reported = await reporter.ReportAsync(remembered, Now, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(5000, reported.Count);
        Assert.Equal("old-2", reported[0]);
        Assert.Equal("9-1", reported[^1]);
    }

    private static HostSettings Host(TemporaryDirectory directory) => new()
    {
        Repository = "owner/repo",
        GitHubToken = "token",
        TelemetryRoot = directory.Combine("telemetry"),
        Machine = "service-host",
    };

    private static void WriteStage(string root, string buildId, string? reference = null, string? sha = null, int? pullRequest = null) =>
        TelemetryWriter.Write(root, TelemetryWriter.StageTable, $"{buildId}.plan.host-1", Now,
            [
                new StageInfo
                {
                    BuildId = buildId,
                    Queue = "PR",
                    Pool = "docker",
                    Machine = "host-1",
                    Stage = "plan",
                    Ref = reference,
                    Sha = sha,
                    PullRequest = pullRequest,
                    StartTime = Now.AddMinutes(-61),
                    EndTime = Now.AddMinutes(-60),
                    DurationMS = 60_000,
                    Succeeded = true,
                },
            ],
            TelemetryJsonContext.Default.StageInfo);

    private sealed class FakeGitHub(string runs, string jobs) : HttpMessageHandler
    {
        public List<string> JobRequests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/jobs", StringComparison.Ordinal))
                JobRequests.Add(path);

            var body = path.EndsWith("/jobs", StringComparison.Ordinal) ? jobs : runs;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
}
