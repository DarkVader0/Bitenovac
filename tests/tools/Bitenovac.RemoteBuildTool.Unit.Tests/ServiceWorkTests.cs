using System.Net;
using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Core.Planning;
using Bitenovac.RemoteBuildTool.Service;
using Bitenovac.RemoteBuildTool.Storage;
using Bitenovac.RemoteBuildTool.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class ServiceWorkTests
{
    private static readonly DateTimeOffset Morning = new(2026, 10, 14, 0, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task PromotionService_ShouldPromoteAMergedPullRequestOnce_WhenItsRunIsStaged()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        Stage(directory, "70-1");
        await using var service = Create(directory, new FakeGitHub());
        var promotion = service.Work<PromotionService>();

        // Act
        await promotion.RunTurnAsync(TestContext.Current.CancellationToken);
        await promotion.RunTurnAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(new LocalVolumeArtifactStore(directory.Combine("main")).Contains(new ProjectId("src/A/A.csproj"), "Debug"));
        Assert.Single(service.Messages, message => message == "PR #7 merged; run 70-1: Promoted 1 entry into main.");
        Assert.Equal([7], ServiceState.Load(directory.Combine("work/state/service.json")).PromotedPullRequests);
    }

    [Fact]
    public async Task PromotionService_ShouldDeleteStagedOutputOlderThanADay_WhenItPolls()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        Stage(directory, "60-1");
        Directory.SetLastWriteTimeUtc(directory.Combine("staging/60-1"), Morning.UtcDateTime.AddHours(-25));
        await using var service = Create(directory, new FakeGitHub());

        // Act
        await service.Work<PromotionService>().RunTurnAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(Directory.Exists(directory.Combine("staging/60-1")));
        Assert.Contains("staged output of run 60-1 expired", service.Messages);
    }

    [Fact]
    public async Task RunTurnAsync_ShouldReportAndCarryOn_WhenTheWorkFails()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        await using var service = Create(directory, new FakeGitHub { Reachable = false });

        TestFactory.WriteFile(directory.Combine("telemetry"), "not a directory");
        service.Time.Now = Morning.AddHours(3);

        // Act
        foreach (var work in service.Provider.GetServices<IHostedService>().OfType<ServiceWork>())
            await work.RunTurnAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(service.Messages, message => message.StartsWith("Settings could not be read, the last ones are kept", StringComparison.Ordinal));
        Assert.Equal(
            ["build records", "housekeeping", "promotion", "rebuild"],
            service.Messages.Where(message => message.Contains(" failed, retried on its next turn", StringComparison.Ordinal))
                .Select(message => message[..message.IndexOf(" failed", StringComparison.Ordinal)])
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task BuildReportService_ShouldRecordEachFinishedBuildOnce_WhenItRunsAgain()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        await using var service = Create(directory, new FakeGitHub());
        var reports = service.Work<BuildReportService>();

        // Act
        await reports.RunTurnAsync(TestContext.Current.CancellationToken);
        await reports.RunTurnAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["5-1"], ServiceState.Load(directory.Combine("work/state/service.json")).ReportedBuilds);
    }

    [Fact]
    public async Task HousekeepingService_ShouldCompactAndPruneOnceADay_WhenItsTimeHasPassed()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        await using var service = Create(directory, new FakeGitHub());
        var housekeeping = service.Work<HousekeepingService>();
        var oldLogs = directory.Combine("logs/PR_1/1-1");
        TestFactory.WriteFile(Path.Combine(oldLogs, "plan.log"), "log");
        Directory.SetLastWriteTimeUtc(oldLogs, Morning.UtcDateTime.AddDays(-40));
        TelemetryWriter.Write(directory.Combine("telemetry"), TelemetryWriter.TargetTable, "1-1.build.host-1", Morning.AddDays(-1),
            [new TargetInfo { Project = "a", Toolchain = "dotnet", Kind = "build", CacheOutcome = "miss" }],
            TelemetryJsonContext.Default.TargetInfo);

        // Act
        await housekeeping.RunTurnAsync(TestContext.Current.CancellationToken);
        var beforeItsTime = File.Exists(directory.Combine("telemetry/telemetry.duckdb"));
        service.Time.Now = Morning.AddHours(3);
        await housekeeping.RunTurnAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(beforeItsTime);
        Assert.True(File.Exists(directory.Combine("telemetry/telemetry.duckdb")));
        Assert.False(Directory.Exists(oldLogs));
        Assert.Contains("compacted target 2026-10-13", service.Messages);
        Assert.Contains(service.Messages, message => message.StartsWith("pruned logs ", StringComparison.Ordinal));
        Assert.Equal(DateOnly.FromDateTime(Morning.UtcDateTime), ServiceState.Load(directory.Combine("work/state/service.json")).HousekeptOn);
    }

    [Fact]
    public async Task RebuildService_ShouldRebuildRightAwayAndOnlyOnce_WhenMainIsEmpty()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        await using var service = Create(directory, new FakeGitHub());
        var rebuild = service.Work<RebuildService>();

        // Act
        await rebuild.RunTurnAsync(TestContext.Current.CancellationToken);
        await rebuild.RunTurnAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DateOnly.FromDateTime(Morning.UtcDateTime), ServiceState.Load(directory.Combine("work/state/service.json")).RebuiltOn);
        Assert.Single(service.Messages, message => message.StartsWith("rebuild of abc: failure", StringComparison.Ordinal));
        var build = Assert.Single(TelemetryWriter.Read(directory.Combine("telemetry"), TelemetryWriter.BuildTable, "rebuild-abc-", TelemetryJsonContext.Default.BuildInfo));
        Assert.Equal("Rebuild", build.Queue);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("""{ "rebuild": null }""", 360)]
    public async Task RebuildService_ShouldNotRebuild_WhenMainHoldsEntriesAndItIsNotDue(string? settings, int minutesLater)
    {
        // Arrange
        using var directory = TestFactory.Directory();
        Stage(directory, "main", store: "main");
        await using var service = Create(directory, new FakeGitHub { Settings = settings });
        service.Time.Now = Morning.AddMinutes(minutesLater);

        // Act
        await service.Work<RebuildService>().RunTurnAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(ServiceState.Load(directory.Combine("work/state/service.json")).RebuiltOn);
    }

    [Fact]
    public async Task StopAsync_ShouldEndEveryService_WhenItWaitsForItsNextTurn()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        Stage(directory, "main", store: "main");
        await using var service = Create(directory, new FakeGitHub());
        var hosted = service.Provider.GetServices<IHostedService>().ToList();
        foreach (var work in hosted)
            await work.StartAsync(TestContext.Current.CancellationToken);
        var waited = new List<bool>();
        foreach (var _ in hosted.OfType<ServiceWork>())
            waited.Add(await service.Time.Waiting.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

        // Act
        foreach (var work in hosted)
            await work.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.All(waited, Assert.True);
        Assert.All(hosted.Cast<BackgroundService>(), work => Assert.True(work.ExecuteTask!.IsCompleted));
        Assert.Equal(5, hosted.Count);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldTakeAnotherTurn_WhenItsWaitEnds()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        Stage(directory, "main", store: "main");
        await using var service = Create(directory, new FakeGitHub());
        service.Time.Wait = TimeSpan.Zero;
        var rebuild = service.Work<RebuildService>();
        await rebuild.StartAsync(TestContext.Current.CancellationToken);

        // Act
        var first = await service.Time.Waiting.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        var second = await service.Time.Waiting.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await rebuild.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first && second);
        Assert.True(rebuild.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task PromotionService_ShouldTakeItsTurnAtOnce_WhenAMergeWakesIt()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        await using var service = Create(directory, new FakeGitHub());
        var promotion = service.Work<PromotionService>();
        await promotion.StartAsync(TestContext.Current.CancellationToken);
        var waiting = await service.Time.Waiting.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        // Act
        service.Provider.GetRequiredService<ServiceWake>().Request();
        var waitingAgain = await service.Time.Waiting.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await promotion.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(waiting && waitingAgain);
        Assert.True(promotion.ExecuteTask!.IsCompletedSuccessfully);
    }

    private static void Stage(TemporaryDirectory directory, string buildId, string store = "staging")
    {
        var root = store == "main" ? directory.Combine("main") : directory.Combine($"{store}/{buildId}");
        var source = directory.Combine($"built/{buildId}");
        TestFactory.WriteFile(Path.Combine(source, "bin", "A.dll"), buildId);
        new LocalVolumeArtifactStore(root).Put(new ProjectId("src/A/A.csproj"), "Debug", source, new StoredTargetHash("own", "full"));
        TestFactory.Plan("Debug", TestFactory.Entry("src/A/A.csproj", ownHash: "own", fullHash: "full")).Save(Path.Combine(root, "plan.json"));
    }

    private static RunningService Create(TemporaryDirectory directory, FakeGitHub github)
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
        var messages = new List<string>();
        var provider = new ServiceCollection()
            .AddLogging(logging => logging.AddProvider(new CapturingLoggerProvider(messages)))
            .AddRemoteBuildToolService(host, ToolCommand.Self, time, github)
            .BuildServiceProvider();
        return new RunningService(provider, time, messages);
    }

    private sealed record RunningService(ServiceProvider Provider, ManualTime Time, List<string> Messages) : IAsyncDisposable
    {
        public T Work<T>() where T : ServiceWork => Provider.GetServices<IHostedService>().OfType<T>().Single();

        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        /// <summary>
        /// Gets a count of the waits started on this clock, each until a work's next turn.
        /// </summary>
        public SemaphoreSlim Waiting { get; } = new(0);

        /// <summary>
        /// Gets or sets how long every wait lasts instead of the time it asks for.
        /// </summary>
        public TimeSpan? Wait { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Waiting.Release();
            return base.CreateTimer(callback, state, Wait ?? dueTime, period);
        }
    }

    private sealed class CapturingLoggerProvider(List<string> messages) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(messages);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(List<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (messages)
                messages.Add(formatter(state, exception));
        }
    }

    private sealed class FakeGitHub : HttpMessageHandler
    {
        public bool Reachable { get; init; } = true;

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
                _ when path.Contains("/branches/", StringComparison.Ordinal) => """{ "commit": { "sha": "abc" } }""",
                _ when path.EndsWith("/pulls", StringComparison.Ordinal) => """
                    [
                      { "number": 7, "merged_at": "2026-10-14T00:20:00Z", "merge_commit_sha": "m7", "head": { "sha": "h7" } },
                      { "number": 8, "merged_at": null, "head": { "sha": "h8" } }
                    ]
                    """,
                _ when query.Contains("head_sha=h7", StringComparison.Ordinal) =>
                    """{ "workflow_runs": [ { "id": 70, "run_attempt": 1, "name": "PR", "event": "pull_request", "conclusion": "success", "updated_at": "2026-10-14T00:10:00Z" } ] }""",
                _ when query.Contains("head_sha=", StringComparison.Ordinal) => """{ "workflow_runs": [] }""",
                _ when path.EndsWith("/repos/owner/repo", StringComparison.Ordinal) => """{ "default_branch": "master" }""",
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
