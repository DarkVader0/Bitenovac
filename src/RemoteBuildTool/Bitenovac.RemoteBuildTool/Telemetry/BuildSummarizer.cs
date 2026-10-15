namespace Bitenovac.RemoteBuildTool.Telemetry;

/// <summary>
/// Represents what a build's own stages cannot see: when it was queued, when it started, and how
/// long its jobs ran.
/// </summary>
/// <param name="Created">When the build was queued.</param>
/// <param name="Started">When its first job started.</param>
/// <param name="QueueTimeMS">How long its jobs waited to start, summed.</param>
/// <param name="RunTimeMS">How long its jobs ran, summed.</param>
/// <param name="Jobs">How many jobs it had.</param>
internal sealed record BuildTimeline(DateTimeOffset? Created, DateTimeOffset? Started, long? QueueTimeMS, long? RunTimeMS, int Jobs)
{
    /// <summary>
    /// Gets a timeline that knows nothing, for a build whose stages are all there is.
    /// </summary>
    public static BuildTimeline Unknown { get; } = new(null, null, null, null, 0);

    /// <summary>
    /// Returns the timeline of a workflow run from its jobs.
    /// </summary>
    /// <param name="jobs">The run's jobs.</param>
    /// <param name="end">When the run ended; a job still running counts until then.</param>
    /// <returns>The timeline; <see cref="Unknown"/> when there are no jobs.</returns>
    public static BuildTimeline FromJobs(IReadOnlyList<WorkflowJob> jobs, DateTimeOffset end)
    {
        if (jobs.Count == 0)
            return Unknown;

        var started = jobs.Where(job => job.StartedAt is not null).ToList();
        return new BuildTimeline(
            jobs.Min(job => job.CreatedAt),
            started.Count > 0 ? started.Min(job => job.StartedAt) : null,
            started.Sum(job => (long)(job.StartedAt!.Value - job.CreatedAt).TotalMilliseconds),
            started.Sum(job => (long)((job.CompletedAt ?? end) - job.StartedAt!.Value).TotalMilliseconds),
            jobs.Count);
    }
}

/// <summary>
/// Provides methods for summing up a build's stages into its <see cref="BuildInfo"/>.
/// </summary>
/// <remarks>
/// <para>Its times, all in milliseconds:</para>
/// <list type="bullet">
/// <item><c>QueueTimeMS</c>: how long the build's jobs waited to start, summed.</item>
/// <item><c>PreparationTimeMS</c>: how long its jobs ran, summed, less the time in stages: checkout and the like.</item>
/// <item><c>E2ETimeMS</c>: from the build being queued to its end.</item>
/// </list>
/// </remarks>
internal static class BuildSummarizer
{
    /// <summary>
    /// Reads a build's stages, samples and test projects from the telemetry share, and writes its
    /// <see cref="BuildInfo"/> there.
    /// </summary>
    /// <param name="root">The telemetry share.</param>
    /// <param name="identity">The build.</param>
    /// <param name="result">The build's result, such as <c>success</c>.</param>
    /// <param name="end">When the build ended.</param>
    /// <param name="timeline">When it was queued and how long its jobs ran.</param>
    /// <param name="writer">The machine writing the record, which names its file.</param>
    /// <returns>The record written.</returns>
    public static BuildInfo Write(string root, BuildIdentity identity, string result, DateTimeOffset end, BuildTimeline timeline, string writer)
    {
        var prefix = identity.BuildId + ".";
        var build = Summarise(
            identity,
            result,
            end,
            timeline,
            [.. TelemetryWriter.Read(root, TelemetryWriter.StageTable, prefix, TelemetryJsonContext.Default.StageInfo)],
            [.. TelemetryWriter.Read(root, TelemetryWriter.MachineTable, prefix, TelemetryJsonContext.Default.MachineSample)],
            [.. TelemetryWriter.Read(root, TelemetryWriter.TargetTable, prefix, TelemetryJsonContext.Default.TargetInfo).Where(target => target.Kind == "test")]);

        TelemetryWriter.Write(root, TelemetryWriter.BuildTable, $"{identity.BuildId}.build.{writer}", end, [build], TelemetryJsonContext.Default.BuildInfo);
        return build;
    }

    /// <summary>
    /// Sums up a build.
    /// </summary>
    /// <param name="identity">The build.</param>
    /// <param name="result">The build's result.</param>
    /// <param name="end">When the build ended.</param>
    /// <param name="timeline">When it was queued and how long its jobs ran.</param>
    /// <param name="stages">Its stages.</param>
    /// <param name="samples">The machine samples its stages took.</param>
    /// <param name="tests">The test projects its stages ran or reused.</param>
    /// <returns>The build's record.</returns>
    public static BuildInfo Summarise(
        BuildIdentity identity,
        string result,
        DateTimeOffset end,
        BuildTimeline timeline,
        IReadOnlyList<StageInfo> stages,
        IReadOnlyList<MachineSample> samples,
        IReadOnlyList<TargetInfo> tests)
    {
        var firstStage = stages.Select(stage => (DateTimeOffset?)stage.StartTime).Min();
        var created = timeline.Created ?? firstStage;
        var stageTime = stages.Sum(stage => stage.DurationMS);
        var builds = stages.Where(stage => stage.Stage.StartsWith("build-", StringComparison.Ordinal)).ToList();
        var cpu = stages.Sum(stage => stage.CpuTimeMS ?? 0);
        var allocated = stages.Sum(stage => stage.AllocatedCpuMS ?? 0);
        var hostCpu = samples.Select(sample => sample.HostCpuUtilization).OfType<double>().ToList();
        var hostMemory = samples.Select(sample => sample.HostMemoryUtilization).OfType<double>().ToList();

        return new BuildInfo
        {
            BuildId = identity.BuildId,
            Queue = identity.Queue,
            Pool = stages.FirstOrDefault()?.Pool ?? identity.Pool,
            Repository = identity.Repository,
            Ref = identity.Ref,
            Sha = identity.Sha,
            PullRequest = identity.PullRequest,
            Result = result,
            CreatedTime = created,
            StartTime = timeline.Started ?? firstStage,
            EndTime = end,
            QueueTimeMS = timeline.QueueTimeMS,
            PreparationTimeMS = timeline.RunTimeMS is { } run ? Math.Max(0, run - stageTime) : null,
            E2ETimeMS = created is { } first ? (long)(end - first).TotalMilliseconds : null,
            Jobs = timeline.Jobs,
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
            LogsPath = identity.LogsPath ?? stages.Select(stage => stage.LogsPath).FirstOrDefault(path => path is not null),
            DropVersion = stages.Select(stage => stage.DropVersion).FirstOrDefault(version => version is not null),
        };
    }
}
