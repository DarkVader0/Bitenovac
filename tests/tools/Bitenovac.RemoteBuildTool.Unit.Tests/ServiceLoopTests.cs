using System.Net;
using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Core.Planning;
using Bitenovac.RemoteBuildTool.Service;
using Bitenovac.RemoteBuildTool.Storage;
using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class ServiceLoopTests
{
    private static readonly DateTimeOffset Morning = new(2026, 10, 14, 0, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("03:30", null, "2026-10-14T03:29:59Z", false)]
    [InlineData("03:30", null, "2026-10-14T03:30:00Z", true)]
    [InlineData("03:30", "2026-10-13", "2026-10-14T23:00:00Z", true)]
    [InlineData("03:30", "2026-10-14", "2026-10-14T23:00:00Z", false)]
    public void IsDue_ShouldBeTrueOncePerDayAfterItsTime_WhenAskedAtAnyTime(string at, string? last, string now, bool expected)
    {
        // Arrange
        var time = TimeOnly.Parse(at, System.Globalization.CultureInfo.InvariantCulture);
        DateOnly? lastRun = last is null ? null : DateOnly.Parse(last, System.Globalization.CultureInfo.InvariantCulture);

        // Act
        var due = ServiceLoop.IsDue(time, lastRun, DateTimeOffset.Parse(now, System.Globalization.CultureInfo.InvariantCulture));

        // Assert
        Assert.Equal(expected, due);
    }

    [Fact]
    public async Task TickAsync_ShouldPromoteAMergedPullRequestOnceAndRecordBuilds_WhenItsRunIsStaged()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        Stage(directory, "70-1");
        var github = new FakeGitHub { Head = "abc" };
        var output = new CapturedOutput();
        var (loop, time, http) = Create(directory, github, output);
        using var client = http;

        // Act
        await loop.TickAsync(TestContext.Current.CancellationToken);
        time.Now = time.Now.AddMinutes(5);
        await loop.TickAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(new LocalVolumeArtifactStore(directory.Combine("main")).Contains(new ProjectId("src/A/A.csproj"), "Debug"));
        Assert.Equal([7], loop.State.PromotedPullRequests);
        Assert.Equal(["5-1"], loop.State.ReportedBuilds);
        Assert.Single(output.Out.Split('\n'), line => line.Contains("PR #7 merged; run 70-1: Promoted 1 entry into main.", StringComparison.Ordinal));
        Assert.Equal([7], ServiceState.Load(directory.Combine("work/state/service.json")).PromotedPullRequests);
    }

    [Fact]
    public async Task TickAsync_ShouldDeleteStagedOutputOlderThanADay_WhenItPolls()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        Stage(directory, "60-1");
        Directory.SetLastWriteTimeUtc(directory.Combine("staging/60-1"), Morning.UtcDateTime.AddHours(-25));
        var output = new CapturedOutput();
        var (loop, _, http) = Create(directory, new FakeGitHub { Head = "abc" }, output);
        using var client = http;

        // Act
        await loop.TickAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(Directory.Exists(directory.Combine("staging/60-1")));
        Assert.Contains("staged output of run 60-1 expired", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TickAsync_ShouldReportAndCarryOn_WhenGitHubCannotBeReached()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var output = new CapturedOutput();
        var (loop, _, http) = Create(directory, new FakeGitHub { Reachable = false }, output);
        using var client = http;

        // Act
        await loop.TickAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("settings failed, retried on its next turn", output.Out, StringComparison.Ordinal);
        Assert.Contains("promotion failed, retried on its next turn", output.Out, StringComparison.Ordinal);
        Assert.Contains("build records failed, retried on its next turn", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TickAsync_ShouldKeepTheDefaults_WhenTheBranchesSettingsAreBroken()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var output = new CapturedOutput();
        var (loop, _, http) = Create(directory, new FakeGitHub { Settings = """{ "pollSeconds": 1 }""" }, output);
        using var client = http;

        // Act
        await loop.TickAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("'pollSeconds' and 'buildReportSeconds' must be at least 10", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TickAsync_ShouldCompactAndPruneOnceADay_WhenItsTimeHasPassed()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var (loop, time, http) = Create(directory, new FakeGitHub(), new CapturedOutput());
        using var client = http;
        var oldLogs = directory.Combine("logs/PR_1/1-1");
        TestFactory.WriteFile(Path.Combine(oldLogs, "plan.log"), "log");
        Directory.SetLastWriteTimeUtc(oldLogs, Morning.UtcDateTime.AddDays(-40));

        // Act
        await loop.TickAsync(TestContext.Current.CancellationToken);
        var beforeItsTime = File.Exists(directory.Combine("telemetry/telemetry.duckdb"));
        time.Now = Morning.AddHours(3);
        await loop.TickAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(beforeItsTime);
        Assert.True(File.Exists(directory.Combine("telemetry/telemetry.duckdb")));
        Assert.False(Directory.Exists(oldLogs));
        Assert.Equal(DateOnly.FromDateTime(Morning.UtcDateTime), loop.State.HousekeptOn);
    }

    [Fact]
    public async Task TickAsync_ShouldAuditTheBranchHeadOnceADay_WhenItsTimeHasPassed()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var (loop, time, http) = Create(directory, new FakeGitHub { Head = "def456" }, new CapturedOutput());
        using var client = http;
        time.Now = Morning.AddMinutes(45);

        // Act
        await loop.TickAsync(TestContext.Current.CancellationToken);
        await loop.CurrentAudit!;
        time.Now = Morning.AddMinutes(50);
        await loop.TickAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DateOnly.FromDateTime(Morning.UtcDateTime), loop.State.AuditedOn);
        Assert.Single(TelemetryWriter.Read(directory.Combine("telemetry"), TelemetryWriter.BuildTable, "cacheaudit-def456-", TelemetryJsonContext.Default.BuildInfo));
    }

    [Fact]
    public async Task TickAsync_ShouldNotAudit_WhenTheBranchTurnsTheAuditOff()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var (loop, time, http) = Create(directory, new FakeGitHub { Settings = """{ "cacheAudit": null }""" }, new CapturedOutput());
        using var client = http;
        time.Now = Morning.AddHours(2);

        // Act
        await loop.TickAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(loop.CurrentAudit);
        Assert.Null(loop.State.AuditedOn);
    }

    [Fact]
    public async Task RunAsync_ShouldReturn_WhenStopped()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var (loop, _, http) = Create(directory, new FakeGitHub(), new CapturedOutput());
        using var client = http;
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Act
        await loop.RunAsync(TimeSpan.FromMilliseconds(20), stop.Token);

        // Assert
        Assert.True(stop.IsCancellationRequested);
    }

    private static void Stage(TemporaryDirectory directory, string buildId)
    {
        var store = directory.Combine($"staging/{buildId}");
        var source = directory.Combine($"built/{buildId}");
        TestFactory.WriteFile(Path.Combine(source, "bin", "A.dll"), buildId);
        new LocalVolumeArtifactStore(store).Put(new ProjectId("src/A/A.csproj"), "Debug", source, new StoredTargetHash("own", "full"));
        TestFactory.Plan("Debug", TestFactory.Entry("src/A/A.csproj", ownHash: "own", fullHash: "full")).Save(Path.Combine(store, "plan.json"));
    }

    private static (ServiceLoop Loop, ManualTime Time, HttpClient Http) Create(TemporaryDirectory directory, FakeGitHub github, CapturedOutput output)
    {
        var host = new HostSettings
        {
            Repository = "owner/repo",
            GitHubToken = "token",
            CloneUrl = directory.Combine("no-such-repository"),
            Machine = "service-host",
            WorkRoot = directory.Combine("work"),
            MainStore = directory.Combine("main"),
            StagingRoot = directory.Combine("staging"),
            LogsRoot = directory.Combine("logs"),
            TelemetryRoot = directory.Combine("telemetry"),
        };
        var time = new ManualTime { Now = Morning };
        var http = GitHubClient.CreateHttpClient("https://api.test", "token", github);
        var loop = new ServiceLoop(host, new GitHubClient(http, host.Repository), ToolCommand.Self, output.Pipeline, time);
        return (loop, time, http);
    }

    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeGitHub : HttpMessageHandler
    {
        public bool Reachable { get; init; } = true;

        public string? Head { get; init; }

        public string? Settings { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var query = request.RequestUri.Query;
            if (!Reachable)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

            var body = path switch
            {
                _ when path.Contains("/contents/", StringComparison.Ordinal) => Settings,
                _ when path.Contains("/branches/", StringComparison.Ordinal) => Head is null ? null : $$"""{ "commit": { "sha": "{{Head}}" } }""",
                _ when path.EndsWith("/pulls", StringComparison.Ordinal) => """
                    [
                      { "number": 7, "merged_at": "2026-10-14T00:20:00Z", "merge_commit_sha": "m7", "head": { "sha": "h7" } },
                      { "number": 8, "merged_at": null, "head": { "sha": "h8" } }
                    ]
                    """,
                _ when query.Contains("head_sha=h7", StringComparison.Ordinal) =>
                    """{ "workflow_runs": [ { "id": 70, "run_attempt": 1, "name": "PR", "event": "pull_request", "conclusion": "success", "updated_at": "2026-10-14T00:10:00Z" } ] }""",
                _ when query.Contains("head_sha=", StringComparison.Ordinal) => """{ "workflow_runs": [] }""",
                _ when path.EndsWith("/actions/runs", StringComparison.Ordinal) =>
                    """{ "workflow_runs": [ { "id": 5, "run_attempt": 1, "name": "PR", "conclusion": "success", "updated_at": "2026-10-14T00:10:00Z" } ] }""",
                _ => """{ "jobs": [] }""",
            };

            return Task.FromResult(body is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
}
