using System.Globalization;
using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Represents the service's work: each tick it starts whatever is due.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A new commit on the watched branch: a cache refresh, which alone fills main.</item>
/// <item>Every few minutes: a <see cref="BuildInfo"/> for each build GitHub finished.</item>
/// <item>Once a day: telemetry compaction and log retention, then the cache audit.</item>
/// </list>
/// <para>
/// One build, refresh or audit, runs at a time, in the background, so records keep coming while
/// it does. A failure of any piece of work is reported and retried on its next turn; it never stops
/// the service. A commit is refreshed once, whatever the outcome; the next commit tries again.
/// </para>
/// </remarks>
internal sealed class ServiceLoop
{
    private readonly HostSettings _host;
    private readonly GitHubClient _github;
    private readonly ServiceBuild _builds;
    private readonly BuildReporter _reporter;
    private readonly PipelineOutput _output;
    private readonly TimeProvider _time;
    private readonly GitWorkspace _refreshWorkspace;
    private readonly GitWorkspace _auditWorkspace;
    private readonly string _statePath;
    private readonly Lock _gate = new();
    private ServiceState _state;
    private Task? _build;
    private DateTimeOffset _nextPoll = DateTimeOffset.MinValue;
    private DateTimeOffset _nextReport = DateTimeOffset.MinValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceLoop"/> class, resuming from its saved state.
    /// </summary>
    /// <param name="host">The machine's settings.</param>
    /// <param name="github">The GitHub API.</param>
    /// <param name="tool">How to start RemoteBuildTool commands.</param>
    /// <param name="output">Where to report what the service does.</param>
    /// <param name="time">The clock.</param>
    public ServiceLoop(HostSettings host, GitHubClient github, ToolCommand tool, PipelineOutput output, TimeProvider time)
    {
        _host = host;
        _github = github;
        _builds = new ServiceBuild(host, tool);
        _reporter = new BuildReporter(github, host);
        _output = output;
        _time = time;
        _refreshWorkspace = new GitWorkspace(Path.Combine(host.WorkRoot, "checkouts", "refresh"), host.EffectiveCloneUrl, host.GitHubToken);
        _auditWorkspace = new GitWorkspace(Path.Combine(host.WorkRoot, "checkouts", "audit"), host.EffectiveCloneUrl, host.GitHubToken);
        _statePath = Path.Combine(host.WorkRoot, "state", "service.json");
        _state = ServiceState.Load(_statePath);
    }

    /// <summary>
    /// Gets the build running now, if any.
    /// </summary>
    public Task? CurrentBuild
    {
        get
        {
            lock (_gate)
                return _build;
        }
    }

    /// <summary>
    /// Gets what the service has done so far.
    /// </summary>
    public ServiceState State
    {
        get
        {
            lock (_gate)
                return _state;
        }
    }

    /// <summary>
    /// Ticks until cancelled, then waits for the running build to stop.
    /// </summary>
    /// <param name="tick">How long to wait between ticks.</param>
    /// <param name="cancellationToken">Stops the service; a running build is cancelled.</param>
    /// <returns>A task that completes when the service has stopped.</returns>
    public async Task RunAsync(TimeSpan tick, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await TickAsync(cancellationToken);
            try
            {
                await Task.Delay(tick, _time, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        if (CurrentBuild is { } build)
            await build;
    }

    /// <summary>
    /// Starts whatever is due now.
    /// </summary>
    /// <param name="cancellationToken">Stops the work started.</param>
    /// <returns>A task that completes once due work is done or, for a build, started.</returns>
    public async Task TickAsync(CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var settings = Settings();

        if (now >= _nextReport)
        {
            _nextReport = now.AddSeconds(settings.BuildReportSeconds);
            await GuardAsync("build records", async () =>
            {
                var reported = await _reporter.ReportAsync(State.ReportedBuilds, now, cancellationToken);
                Update(state => state with { ReportedBuilds = reported });
            });
        }

        if (now >= _nextPoll && Idle)
        {
            _nextPoll = now.AddSeconds(settings.PollSeconds);
            await GuardAsync("cache refresh", async () =>
            {
                var head = await _github.BranchHeadAsync(settings.Branch, cancellationToken);
                if (head != State.RefreshedSha)
                {
                    Update(state => state with { RefreshedSha = head });
                    Start(ServiceBuildKind.CacheRefresh, _refreshWorkspace, settings.Branch, head, now, cancellationToken);
                }
            });
        }

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        if (IsDue(settings.Housekeeping, State.HousekeptOn, now))
        {
            Update(state => state with { HousekeptOn = today });
            await GuardAsync("housekeeping", () =>
            {
                foreach (var day in TelemetryCompactor.Compact(_host.TelemetryRoot, today))
                    Report($"compacted {day}");
                foreach (var folder in LogRetention.Prune(_host.LogsRoot, settings.LogRetentionDays, now))
                    Report($"pruned logs {folder}");
                return Task.CompletedTask;
            });
        }

        if (settings.CacheAudit is { } audit && IsDue(audit, State.AuditedOn, now) && Idle)
        {
            Update(state => state with { AuditedOn = today });
            await GuardAsync("cache audit", async () =>
            {
                var head = await _github.BranchHeadAsync(settings.Branch, cancellationToken);
                Start(ServiceBuildKind.CacheAudit(settings.CacheAuditConfiguration), _auditWorkspace, settings.Branch, head, now, cancellationToken);
            });
        }
    }

    /// <summary>
    /// Determines whether daily work set for <paramref name="at"/> is due.
    /// </summary>
    /// <param name="at">The time of day, in UTC, the work is set for.</param>
    /// <param name="last">The day it last ran.</param>
    /// <param name="now">The current time.</param>
    /// <returns><see langword="true"/> once the time has passed on a day it has not run.</returns>
    public static bool IsDue(TimeOnly at, DateOnly? last, DateTimeOffset now) =>
        last != DateOnly.FromDateTime(now.UtcDateTime) && TimeOnly.FromDateTime(now.UtcDateTime) >= at;

    private bool Idle => CurrentBuild is null or { IsCompleted: true };

    // The watched branch's own settings decide; until it is checked out, or when they are broken,
    // the defaults do.
    private RepositorySettings Settings()
    {
        try
        {
            return RepositorySettings.Load(_refreshWorkspace.Directory);
        }
        catch (InvalidOperationException exception)
        {
            Report($"settings: {exception.Message}; using the defaults");
            return new RepositorySettings();
        }
    }

    private void Start(ServiceBuildKind kind, GitWorkspace workspace, string branch, string sha, DateTimeOffset detected, CancellationToken cancellationToken)
    {
        Report($"{kind.Queue} of {sha} started");
        var build = Task.Run(async () =>
        {
            try
            {
                var info = await _builds.RunAsync(kind, workspace, branch, sha, detected, cancellationToken);
                Report(string.Create(CultureInfo.InvariantCulture,
                    $"{kind.Queue} of {sha}: {info.Result} in {info.StageTimeMS / 1000.0:0.0} s; logs in {info.LogsPath}"));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                Report($"{kind.Queue} of {sha} failed: {exception.Message}");
            }
        }, CancellationToken.None);

        lock (_gate)
            _build = build;
    }

    private async Task GuardAsync(string work, Func<Task> run)
    {
        try
        {
            await run();
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or UnauthorizedAccessException
            or InvalidOperationException or TaskCanceledException or DuckDB.NET.Data.DuckDBException)
        {
            Report($"{work} failed, retried on its next turn: {exception.Message}");
        }
    }

    private void Update(Func<ServiceState, ServiceState> change)
    {
        lock (_gate)
        {
            _state = change(_state);
            _state.Save(_statePath);
        }
    }

    private void Report(string message) =>
        _output.WriteLine($"{_time.GetUtcNow().ToString("u", CultureInfo.InvariantCulture)} {message}");
}
