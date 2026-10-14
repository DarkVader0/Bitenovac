using System.Diagnostics;
using Bitenovac.RemoteBuildTool.Core.Processes;

namespace Bitenovac.RemoteBuildTool.Telemetry;

/// <summary>
/// Specifies a part of a stage whose time <see cref="StageInfo"/> reports on its own.
/// </summary>
internal enum StagePhase
{
    Evaluation,
    Restore,
    Hashing,
    CacheLookup,
    CacheRestore,
    Build,
    Test,
    Coverage,
    Publish,
    Pack,
    Store,
}

/// <summary>
/// Collects what one stage did and, when it ends, writes it to the telemetry share: its
/// <see cref="StageInfo"/>, a <see cref="TargetInfo"/> per project it handled, a
/// <see cref="TestInfo"/> per test case it ran, and the <see cref="MachineSample"/>s taken while it ran.
/// </summary>
/// <remarks>
/// Without a telemetry share the stage is still measured, so the same code runs everywhere, and
/// nothing is written. A stage killed before it ends writes nothing either.
/// </remarks>
internal sealed class StageRecorder : IDisposable
{
    /// <summary>
    /// How often the machine is sampled while a stage runs.
    /// </summary>
    public static readonly TimeSpan DefaultSampleInterval = TimeSpan.FromSeconds(30);

    private readonly PipelineOptions _options;
    private readonly PipelineOutput _output;
    private readonly BuildIdentity _identity;
    private readonly string _stage;
    private readonly string? _configuration;
    private readonly Func<MachineCounters> _readCounters;
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly long?[] _phases = new long?[Enum.GetValues<StagePhase>().Length];
    private readonly List<TargetInfo> _targets = [];
    private readonly List<TestInfo> _tests = [];
    private readonly List<ResourceUsage> _processes = [];
    private readonly List<MachineSample> _samples = [];
    private readonly Lock _gate = new();
    private readonly Timer _sampler;
    private MachineCounters _lastCounters;
    private int? _projects;
    private int? _cacheHits;
    private string? _dropVersion;
    private bool _completed;

    private StageRecorder(
        PipelineOptions options, PipelineOutput output, string stage, string? configuration, TimeSpan sampleInterval, Func<MachineCounters> readCounters)
    {
        _options = options;
        _output = output;
        _identity = options.Identity ?? BuildIdentity.FromEnvironment(_ => null);
        _stage = stage;
        _configuration = configuration;
        _readCounters = readCounters;
        _lastCounters = readCounters();
        Sample(_lastCounters, _lastCounters);
        _sampler = new Timer(_ => SampleNow(), null, sampleInterval, sampleInterval);
    }

    /// <summary>
    /// Gets the stage's name as its records and files carry it, such as <c>build-Debug</c>.
    /// </summary>
    public string Name => _configuration is null ? _stage : $"{_stage}-{_configuration}";

    /// <summary>
    /// Starts recording a stage.
    /// </summary>
    /// <param name="options">The run's options, which name the telemetry share and the build.</param>
    /// <param name="output">The output whose first failure the stage reports.</param>
    /// <param name="stage">The command, such as <c>build</c>.</param>
    /// <param name="configuration">The configuration the command runs for, if it runs for one.</param>
    /// <param name="sampleInterval">How often to sample the machine; <see cref="DefaultSampleInterval"/> when omitted.</param>
    /// <param name="readCounters">Reads the machine's counters; <see cref="MachineCounters.Read"/> when omitted.</param>
    /// <returns>The recorder, which writes the stage when it completes or is disposed.</returns>
    public static StageRecorder Start(
        PipelineOptions options,
        PipelineOutput output,
        string stage,
        string? configuration = null,
        TimeSpan? sampleInterval = null,
        Func<MachineCounters>? readCounters = null) =>
        new(options, output, stage, configuration, sampleInterval ?? DefaultSampleInterval, readCounters ?? (() => MachineCounters.Read()));

    /// <summary>
    /// Returns the test kind a test project's name implies, from the segment before <c>.Tests</c>.
    /// </summary>
    /// <param name="projectName">The test project's name, as in <c>Lib.Integration.Tests</c>.</param>
    /// <returns><c>Unit</c>, <c>Integration</c>, <c>E2E</c> or <c>Api</c>; otherwise <c>Other</c>.</returns>
    public static string TestKindOf(string projectName)
    {
        var segments = projectName.Split('.');
        return segments.Length >= 3 && segments[^1].Equals("Tests", StringComparison.OrdinalIgnoreCase)
            ? segments[^2] switch
            {
                var kind when kind.Equals("Unit", StringComparison.OrdinalIgnoreCase) => "Unit",
                var kind when kind.Equals("Integration", StringComparison.OrdinalIgnoreCase) => "Integration",
                var kind when kind.Equals("E2E", StringComparison.OrdinalIgnoreCase) => "E2E",
                var kind when kind.Equals("Api", StringComparison.OrdinalIgnoreCase) => "Api",
                _ => "Other",
            }
            : "Other";
    }

    /// <summary>
    /// Starts timing a phase; the time is added to the phase when the returned scope is disposed.
    /// </summary>
    /// <param name="phase">The phase.</param>
    /// <returns>The scope.</returns>
    public PhaseScope Measure(StagePhase phase) => new(this, phase);

    /// <summary>
    /// Adds time to a phase.
    /// </summary>
    /// <param name="phase">The phase.</param>
    /// <param name="elapsed">The time to add.</param>
    public void Add(StagePhase phase, TimeSpan elapsed)
    {
        lock (_gate)
            _phases[(int)phase] = (_phases[(int)phase] ?? 0) + (long)elapsed.TotalMilliseconds;
    }

    /// <summary>
    /// Records what a process the stage started used.
    /// </summary>
    /// <param name="usage">What it used.</param>
    public void AddProcess(ResourceUsage usage)
    {
        lock (_gate)
            _processes.Add(usage);
    }

    /// <summary>
    /// Records how many projects the stage covered and how many of them were cache hits.
    /// </summary>
    /// <param name="projects">The projects.</param>
    /// <param name="cacheHits">The cache hits among them.</param>
    public void SetProjects(int projects, int cacheHits)
    {
        _projects = projects;
        _cacheHits = cacheHits;
    }

    /// <summary>
    /// Records the version the stage dropped, for a release.
    /// </summary>
    /// <param name="version">The drop's version, as in <c>2026.10.14.001</c>.</param>
    public void SetDropVersion(string version) => _dropVersion = version;

    /// <summary>
    /// Records what the stage did with one project.
    /// </summary>
    /// <param name="target">The project's record; the build and stage fields are filled in here.</param>
    public void AddTarget(TargetInfo target)
    {
        lock (_gate)
        {
            _targets.Add(target with
            {
                BuildId = _identity.BuildId,
                Queue = _identity.Queue,
                Pool = _identity.Pool,
                Machine = _identity.Machine,
                Stage = Name,
                Configuration = _configuration,
                Sha = _identity.Sha,
                PullRequest = _identity.PullRequest,
                LogsPath = _identity.LogsPath,
            });
        }
    }

    /// <summary>
    /// Records the test cases of one test project's run.
    /// </summary>
    /// <param name="tests">The test cases; the build and stage fields are filled in here.</param>
    public void AddTests(IEnumerable<TestInfo> tests)
    {
        lock (_gate)
        {
            _tests.AddRange(tests.Select(test => test with
            {
                BuildId = _identity.BuildId,
                Queue = _identity.Queue,
                Pool = _identity.Pool,
                Machine = _identity.Machine,
                Stage = Name,
                Configuration = _configuration,
                Sha = _identity.Sha,
                PullRequest = _identity.PullRequest,
            }));
        }
    }

    /// <summary>
    /// Ends the stage and writes its records.
    /// </summary>
    /// <param name="exitCode">The command's exit code.</param>
    /// <returns><paramref name="exitCode"/>, so a command can return through this.</returns>
    public int Complete(int exitCode)
    {
        lock (_gate)
        {
            if (_completed)
                return exitCode;

            _completed = true;
        }

        _sampler.Dispose();
        var counters = _readCounters();
        Sample(_lastCounters, counters);

        var stage = Summarise(exitCode, counters);
        if (_options.TelemetryRoot is { } root)
        {
            var name = $"{_identity.BuildId}.{Name}.{_identity.Machine}";
            TelemetryWriter.Write(root, TelemetryWriter.StageTable, name, _startTime, [stage], TelemetryJsonContext.Default.StageInfo);
            TelemetryWriter.Write(root, TelemetryWriter.TargetTable, name, _startTime, _targets, TelemetryJsonContext.Default.TargetInfo);
            TelemetryWriter.Write(root, TelemetryWriter.TestTable, name, _startTime, _tests, TelemetryJsonContext.Default.TestInfo);
            TelemetryWriter.Write(root, TelemetryWriter.MachineTable, name, _startTime, _samples, TelemetryJsonContext.Default.MachineSample);
        }

        return exitCode;
    }

    /// <summary>
    /// Ends the stage as failed if it has not completed, such as when the command threw.
    /// </summary>
    public void Dispose() => Complete(1);

    private StageInfo Summarise(int exitCode, MachineCounters counters)
    {
        var duration = (long)_stopwatch.Elapsed.TotalMilliseconds;
        var self = Process.GetCurrentProcess();

        var cpu = counters.ContainerCpuTime
            ?? self.TotalProcessorTime + _processes.Aggregate(TimeSpan.Zero, (sum, usage) => sum + (usage.CpuTime ?? TimeSpan.Zero));
        var allocatedCpus = counters.ContainerCpuLimit ?? Environment.ProcessorCount;
        var allocatedCpuMS = (long)(allocatedCpus * duration);
        var peakMemory = counters.ContainerPeakMemoryBytes
            ?? _processes.Select(usage => usage.PeakMemoryBytes ?? 0).Append(self.PeakWorkingSet64).Max();
        var memoryLimit = counters.ContainerMemoryLimitBytes ?? counters.HostMemoryTotalBytes;

        long? Phase(StagePhase phase) => _phases[(int)phase];

        return new StageInfo
        {
            BuildId = _identity.BuildId,
            Queue = _identity.Queue,
            Pool = _identity.Pool,
            Machine = _identity.Machine,
            Job = _identity.Job,
            Stage = Name,
            Configuration = _configuration,
            Ref = _identity.Ref,
            Sha = _identity.Sha,
            PullRequest = _identity.PullRequest,
            StartTime = _startTime,
            EndTime = _startTime + _stopwatch.Elapsed,
            DurationMS = duration,
            ExitCode = exitCode,
            Succeeded = exitCode == 0,
            FirstError = _output.FirstFailure,
            Projects = _projects,
            CacheHits = _cacheHits,
            CacheMisses = _projects - _cacheHits,
            EvaluationMS = Phase(StagePhase.Evaluation),
            RestoreMS = Phase(StagePhase.Restore),
            HashingMS = Phase(StagePhase.Hashing),
            CacheLookupMS = Phase(StagePhase.CacheLookup),
            CacheRestoreMS = Phase(StagePhase.CacheRestore),
            BuildMS = Phase(StagePhase.Build),
            TestMS = Phase(StagePhase.Test),
            CoverageMS = Phase(StagePhase.Coverage),
            PublishMS = Phase(StagePhase.Publish),
            PackMS = Phase(StagePhase.Pack),
            StoreMS = Phase(StagePhase.Store),
            CpuTimeMS = (long)cpu.TotalMilliseconds,
            AllocatedCpus = allocatedCpus,
            AllocatedCpuMS = allocatedCpuMS,
            CpuUtilization = allocatedCpuMS > 0 ? cpu.TotalMilliseconds / allocatedCpuMS : null,
            PeakMemoryBytes = peakMemory,
            MemoryLimitBytes = memoryLimit,
            MemoryUtilization = memoryLimit > 0 ? (double)peakMemory / memoryLimit : null,
            LogsPath = _identity.LogsPath,
            DropVersion = _dropVersion,
        };
    }

    private void SampleNow()
    {
        lock (_gate)
        {
            if (_completed)
                return;

            var counters = _readCounters();
            Sample(_lastCounters, counters);
            _lastCounters = counters;
        }
    }

    private void Sample(MachineCounters earlier, MachineCounters now) =>
        _samples.Add(new MachineSample
        {
            BuildId = _identity.BuildId,
            Queue = _identity.Queue,
            Pool = _identity.Pool,
            Machine = _identity.Machine,
            Stage = Name,
            Time = DateTimeOffset.UtcNow,
            HostCpuUtilization = now.HostCpuUtilizationSince(earlier),
            HostMemoryUsedBytes = now.HostMemoryUsedBytes,
            HostMemoryTotalBytes = now.HostMemoryTotalBytes,
            HostMemoryUtilization = now.HostMemoryTotalBytes > 0 ? (double?)now.HostMemoryUsedBytes / now.HostMemoryTotalBytes : null,
            ContainerCpuMS = now.ContainerCpuTime is { } cpu ? (long)cpu.TotalMilliseconds : null,
            ContainerMemoryBytes = now.ContainerMemoryBytes,
            ContainerMemoryLimitBytes = now.ContainerMemoryLimitBytes,
        });

    /// <summary>
    /// Represents a phase being timed; disposing it adds the time to the phase.
    /// </summary>
    internal readonly struct PhaseScope : IDisposable
    {
        private readonly StageRecorder _recorder;
        private readonly StagePhase _phase;
        private readonly long _started;

        public PhaseScope(StageRecorder recorder, StagePhase phase)
        {
            _recorder = recorder;
            _phase = phase;
            _started = Stopwatch.GetTimestamp();
        }

        public void Dispose() => _recorder.Add(_phase, Stopwatch.GetElapsedTime(_started));
    }
}
