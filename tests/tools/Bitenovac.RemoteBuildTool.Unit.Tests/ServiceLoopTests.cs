using System.Net;
using Bitenovac.RemoteBuildTool.Service;
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
    public async Task TickAsync_ShouldRecordBuildsAndRefreshANewCommitOnce_WhenTheBranchMoves()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var github = new FakeGitHub { Head = "abc123" };
        var output = new CapturedOutput();
        var (loop, time, http) = Create(directory, github, output);
        using var client = http;

        // Act
        await loop.TickAsync(TestContext.Current.CancellationToken);
        await loop.CurrentBuild!;
        time.Now = time.Now.AddMinutes(5);
        await loop.TickAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("abc123", loop.State.RefreshedSha);
        Assert.Equal(["5-1"], loop.State.ReportedBuilds);
        var refresh = Assert.Single(TelemetryWriter.Read(directory.Combine("telemetry"), TelemetryWriter.BuildTable, "cacherefresh-abc123-", TelemetryJsonContext.Default.BuildInfo));
        Assert.Equal("Cache refresh", refresh.Queue);
        Assert.Equal("failure", refresh.Result);
        Assert.Equal("abc123", refresh.Sha);
        Assert.Single(output.Out.Split('\n'), line => line.Contains("Cache refresh of abc123 started", StringComparison.Ordinal));
        Assert.Equal(ServiceState.Load(directory.Combine("work/state/service.json")).RefreshedSha, loop.State.RefreshedSha);
    }

    [Fact]
    public async Task TickAsync_ShouldReportAndCarryOn_WhenGitHubCannotBeReached()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var github = new FakeGitHub { Head = null };
        var output = new CapturedOutput();
        var (loop, _, http) = Create(directory, github, output);
        using var client = http;

        // Act
        await loop.TickAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(loop.CurrentBuild);
        Assert.Contains("cache refresh failed, retried on its next turn", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TickAsync_ShouldCompactAndPruneOnceADay_WhenItsTimeHasPassed()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var github = new FakeGitHub { Head = null };
        var (loop, time, http) = Create(directory, github, new CapturedOutput());
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
    public async Task TickAsync_ShouldAuditOnceADay_WhenItsTimeHasPassedAndNoBuildRuns()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var github = new FakeGitHub { Head = "def456" };
        var (loop, time, http) = Create(directory, github, new CapturedOutput());
        using var client = http;
        await loop.TickAsync(TestContext.Current.CancellationToken);
        await loop.CurrentBuild!;
        time.Now = Morning.AddMinutes(45);

        // Act
        await loop.TickAsync(TestContext.Current.CancellationToken);
        await loop.CurrentBuild!;

        // Assert
        Assert.Equal(DateOnly.FromDateTime(Morning.UtcDateTime), loop.State.AuditedOn);
        Assert.Single(TelemetryWriter.Read(directory.Combine("telemetry"), TelemetryWriter.BuildTable, "cacheaudit-def456-", TelemetryJsonContext.Default.BuildInfo));
    }

    [Fact]
    public async Task TickAsync_ShouldUseTheDefaults_WhenTheBranchesSettingsAreBroken()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestFactory.WriteFile(directory.Combine("work/checkouts/refresh/remotebuildtool.json"), """{ "pollSeconds": 1 }""");
        var output = new CapturedOutput();
        var (loop, _, http) = Create(directory, new FakeGitHub { Head = null }, output);
        using var client = http;

        // Act
        await loop.TickAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("using the defaults", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ShouldReturn_WhenStopped()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var (loop, _, http) = Create(directory, new FakeGitHub { Head = null }, new CapturedOutput());
        using var client = http;
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Act
        await loop.RunAsync(TimeSpan.FromMilliseconds(20), stop.Token);

        // Assert
        Assert.True(stop.IsCancellationRequested);
    }

    private static (ServiceLoop Loop, ManualTime Time, HttpClient Http) Create(TemporaryDirectory directory, FakeGitHub github, CapturedOutput output)
    {
        TestFactory.WriteFile(directory.Combine("work/checkouts/audit/remotebuildtool.json"), "{}");
        var host = new HostSettings
        {
            Repository = "owner/repo",
            GitHubToken = "token",
            CloneUrl = directory.Combine("no-such-repository"),
            Machine = "service-host",
            WorkRoot = directory.Combine("work"),
            MainStore = directory.Combine("main"),
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
        public string? Head { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = path switch
            {
                _ when path.Contains("/branches/", StringComparison.Ordinal) => Head is null ? null : $$"""{ "commit": { "sha": "{{Head}}" } }""",
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
