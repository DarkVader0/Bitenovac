using System.Globalization;
using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Represents the service's work: each tick it starts whatever is due.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Every poll: the staged output of each newly merged pull request is promoted into main, and
/// staged output older than a day is deleted. See <see cref="PullRequestPromoter"/>.</item>
/// <item>Every few minutes: a <see cref="BuildInfo"/> for each build GitHub finished.</item>
/// <item>Once a day: telemetry compaction and log retention, and the cache audit.</item>
/// </list>
/// <para>
/// The repository's settings are read from its default branch every poll. The audit runs in the
/// background, so everything else carries on while it does. A failure of any piece of work is
/// reported and retried on its next turn; it never stops the service.
/// </para>
/// </remarks>
internal sealed class ServiceLoop
{
    private readonly HostSettings _host;
    private readonly GitHubClient _github;
    private readonly ServiceBuild _builds;
    private readonly BuildReporter _reporter;
    private readonly PullRequestPromoter _promoter;
    private readonly PipelineOutput _output;
    private readonly TimeProvider _time;
    private readonly GitWorkspace _auditWorkspace;
    private readonly string _statePath;
    private readonly Lock _gate = new();
    private ServiceState _state;
    private RepositorySettings _settings = new();
    private Task? _audit;
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
        _promoter = new PullRequestPromoter(github, host);
        _output = output;
        _time = time;
        _auditWorkspace = new GitWorkspace(Path.Combine(host.WorkRoot, "checkouts", "audit"), host.EffectiveCloneUrl, host.GitHubToken);
        _statePath = Path.Combine(host.WorkRoot, "state", "service.json");
        _state = ServiceState.Load(_statePath);
    }

    /// <summary>
    /// Gets the cache audit running now, if any.
    /// </summary>
    public Task? CurrentAudit
    {
        get
        {
            lock (_gate)
                return _audit;
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
    /// Ticks until cancelled, then waits for a running audit to stop.
    /// </summary>
    /// <param name="tick">How long to wait between ticks.</param>
    /// <param name="cancellationToken">Stops the service; a running audit is cancelled.</param>
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

        if (CurrentAudit is { } audit)
            await audit;
    }

    /// <summary>
    /// Starts whatever is due now.
    /// </summary>
    /// <param name="cancellationToken">Stops the work started.</param>
    /// <returns>A task that completes once due work is done or, for the audit, started.</returns>
    public async Task TickAsync(CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();

        if (now >= _nextPoll)
        {
            _nextPoll = now.AddSeconds(_settings.PollSeconds);
            await GuardAsync("settings", async () =>
                _settings = RepositorySettings.Parse(
                    await _github.DefaultBranchFileAsync(RepositorySettings.FileName, cancellationToken),
                    $"{RepositorySettings.FileName} on the default branch"));

            await GuardAsync("promotion", async () =>
            {
                foreach (var run in StagingRetention.Prune(_host.StagingRoot, now))
                    Report($"staged output of run {run} expired");

                var promoted = await _promoter.PromoteAsync(_settings.Branch, State.PromotedPullRequests, now, Report, cancellationToken);
                Update(state => state with { PromotedPullRequests = promoted });
            });
        }

        if (now >= _nextReport)
        {
            _nextReport = now.AddSeconds(_settings.BuildReportSeconds);
            await GuardAsync("build records", async () =>
            {
                var reported = await _reporter.ReportAsync(State.ReportedBuilds, now, cancellationToken);
                Update(state => state with { ReportedBuilds = reported });
            });
        }

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        if (IsDue(_settings.Housekeeping, State.HousekeptOn, now))
        {
            Update(state => state with { HousekeptOn = today });
            await GuardAsync("housekeeping", () =>
            {
                foreach (var day in TelemetryCompactor.Compact(_host.TelemetryRoot, today))
                    Report($"compacted {day}");
                foreach (var folder in LogRetention.Prune(_host.LogsRoot, _settings.LogRetentionDays, now))
                    Report($"pruned logs {folder}");
                return Task.CompletedTask;
            });
        }

        if (_settings.CacheAudit is { } audit && IsDue(audit, State.AuditedOn, now) && CurrentAudit is null or { IsCompleted: true })
        {
            Update(state => state with { AuditedOn = today });
            await GuardAsync("cache audit", async () =>
            {
                var head = await _github.BranchHeadAsync(_settings.Branch, cancellationToken);
                StartAudit(ServiceBuildKind.CacheAudit(_settings.CacheAuditConfiguration), _settings.Branch, head, now, cancellationToken);
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

    private void StartAudit(ServiceBuildKind kind, string branch, string sha, DateTimeOffset detected, CancellationToken cancellationToken)
    {
        Report($"{kind.Queue} of {sha} started");
        var audit = Task.Run(async () =>
        {
            try
            {
                var info = await _builds.RunAsync(kind, _auditWorkspace, branch, sha, detected, cancellationToken);
                Report(string.Create(CultureInfo.InvariantCulture,
                    $"{kind.Queue} of {sha}: {info.Result} in {info.StageTimeMS / 1000.0:0.0} s; logs in {info.LogsPath}"));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                Report($"{kind.Queue} of {sha} failed: {exception.Message}");
            }
        }, CancellationToken.None);

        lock (_gate)
            _audit = audit;
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
