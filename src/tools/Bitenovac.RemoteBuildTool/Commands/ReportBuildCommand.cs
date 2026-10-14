using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Commands;

/// <summary>
/// Provides the <c>report-build</c> command, which sums up a build's stages into its
/// <see cref="BuildInfo"/> once every other job of the build has ended.
/// </summary>
/// <remarks>
/// <para>
/// The stages already in the telemetry share give the build's resources, cache hits and failures.
/// The workflow's job list, when given, adds what no stage sees: how long jobs waited for a runner
/// and how much of each job went to anything but RemoteBuildTool, such as checkout.
/// </para>
/// <para>Its definitions, all in milliseconds:</para>
/// <list type="bullet">
/// <item><c>QueueTimeMS</c>: the sum over jobs of the wait from queued to started.</item>
/// <item><c>PreparationTimeMS</c>: the sum over jobs of their run time, less the time in stages.</item>
/// <item><c>E2ETimeMS</c>: from the first job queued to this report.</item>
/// </list>
/// </remarks>
internal static class ReportBuildCommand
{
    /// <summary>
    /// Writes the build's <see cref="BuildInfo"/>.
    /// </summary>
    /// <param name="options">The run's options, which name the telemetry share and the build.</param>
    /// <param name="output">Where to report.</param>
    /// <param name="result">The build's result as the workflow sees it, such as <c>success</c>.</param>
    /// <param name="jobsPath">The workflow's job list from the GitHub REST API, or <see langword="null"/>.</param>
    /// <param name="now">When the build ends; the current time when omitted.</param>
    /// <returns>The exit code.</returns>
    public static int Run(PipelineOptions options, PipelineOutput output, string result, string? jobsPath, DateTimeOffset? now = null)
    {
        if (options.TelemetryRoot is not { } root)
        {
            output.WriteLine("No telemetry share (REMOTEBUILDTOOL_TELEMETRY_DIR); nothing to report.");
            return 0;
        }

        var identity = options.Identity ?? BuildIdentity.FromEnvironment(_ => null);
        var prefix = identity.BuildId + ".";
        var stages = TelemetryWriter.Read(root, TelemetryWriter.StageTable, prefix, TelemetryJsonContext.Default.StageInfo).ToList();
        var samples = TelemetryWriter.Read(root, TelemetryWriter.MachineTable, prefix, TelemetryJsonContext.Default.MachineSample).ToList();
        var tests = TelemetryWriter.Read(root, TelemetryWriter.TargetTable, prefix, TelemetryJsonContext.Default.TargetInfo)
            .Where(target => target.Kind == "test")
            .ToList();
        var jobs = jobsPath is not null && File.Exists(jobsPath) ? WorkflowJob.Parse(File.ReadAllText(jobsPath)) : [];

        var end = now ?? DateTimeOffset.UtcNow;
        var created = jobs.Count > 0 ? jobs.Min(job => job.CreatedAt) : stages.Select(stage => (DateTimeOffset?)stage.StartTime).Min();
        var started = jobs.Any(job => job.StartedAt is not null)
            ? jobs.Min(job => job.StartedAt)
            : stages.Select(stage => (DateTimeOffset?)stage.StartTime).Min();
        var stageTime = stages.Sum(stage => stage.DurationMS);
        var builds = stages.Where(stage => stage.Stage.StartsWith("build-", StringComparison.Ordinal)).ToList();
        var cpu = stages.Sum(stage => stage.CpuTimeMS ?? 0);
        var allocated = stages.Sum(stage => stage.AllocatedCpuMS ?? 0);
        var hostCpu = samples.Select(sample => sample.HostCpuUtilization).OfType<double>().ToList();
        var hostMemory = samples.Select(sample => sample.HostMemoryUtilization).OfType<double>().ToList();

        var build = new BuildInfo
        {
            BuildId = identity.BuildId,
            Queue = identity.Queue,
            Pool = identity.Pool,
            Repository = identity.Repository,
            Ref = identity.Ref,
            Sha = identity.Sha,
            PullRequest = identity.PullRequest,
            Result = result,
            CreatedTime = created,
            StartTime = started,
            EndTime = end,
            QueueTimeMS = jobs.Count > 0
                ? jobs.Sum(job => job.StartedAt is { } jobStarted ? (long)(jobStarted - job.CreatedAt).TotalMilliseconds : 0)
                : null,
            PreparationTimeMS = jobs.Count > 0
                ? Math.Max(0, jobs.Sum(job => job.StartedAt is { } jobStarted ? (long)((job.CompletedAt ?? end) - jobStarted).TotalMilliseconds : 0) - stageTime)
                : null,
            E2ETimeMS = created is { } first ? (long)(end - first).TotalMilliseconds : null,
            Jobs = jobs.Count,
            Stages = stages.Count,
            StageTimeMS = stageTime,
            CpuTimeMS = stages.Count > 0 ? cpu : null,
            AllocatedCpuMS = stages.Count > 0 ? allocated : null,
            CpuUtilization = allocated > 0 ? (double)cpu / allocated : null,
            PeakMemoryBytes = stages.Max(stage => stage.PeakMemoryBytes),
            MemoryUtilization = stages.Max(stage => stage.MemoryUtilization),
            HostCpuUtilization = hostCpu.Count > 0 ? hostCpu.Average() : null,
            HostMemoryUtilization = hostMemory.Count > 0 ? hostMemory.Average() : null,
            MachinesUsed = stages.Select(stage => stage.Machine).Distinct(StringComparer.Ordinal).Count(),
            CacheHits = builds.Count > 0 ? builds.Sum(stage => stage.CacheHits ?? 0) : null,
            CacheMisses = builds.Count > 0 ? builds.Sum(stage => stage.CacheMisses ?? 0) : null,
            TestsFailed = tests.Count > 0 ? tests.Sum(test => test.TestsFailed ?? 0) : null,
            FirstError = stages.Where(stage => !stage.Succeeded).OrderBy(stage => stage.StartTime).Select(stage => stage.FirstError).FirstOrDefault(),
            LogsPath = identity.LogsPath,
        };

        TelemetryWriter.Write(root, TelemetryWriter.BuildTable, $"{identity.BuildId}.build.{identity.Machine}", end, [build], TelemetryJsonContext.Default.BuildInfo);
        output.WriteLine($"Build {build.BuildId} ({result}): {stages.Count} stage(s) over {build.MachinesUsed} machine(s), {jobs.Count} job(s).");
        return 0;
    }
}
