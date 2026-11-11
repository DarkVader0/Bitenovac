using System.Diagnostics;
using System.Globalization;
using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Core.Scheduling;
using Bitenovac.RemoteBuildTool.Planning;
using Bitenovac.RemoteBuildTool.Telemetry;
using Bitenovac.RemoteBuildTool.Toolchains;

namespace Bitenovac.RemoteBuildTool.Commands;

/// <summary>
/// Provides the part of the build stage that builds several units at once.
/// </summary>
/// <remarks>
/// <para>
/// The units of a schedule do not depend on each other, so each is a build of its own. Every slot
/// takes the next unit in the schedule's order, the longest chain first, until none is left.
/// </para>
/// <para>
/// A unit that fails does not stop the others: they share nothing with it, and their failures, if
/// any, are worth knowing in the same run.
/// </para>
/// </remarks>
internal static class ConcurrentBuild
{
    /// <summary>
    /// Builds every unit of <paramref name="schedule"/>, as many at once as it has slots.
    /// </summary>
    /// <param name="toolchain">The toolchain that owns the projects.</param>
    /// <param name="options">The options of this run.</param>
    /// <param name="entries">The plan entries of the schedule's projects.</param>
    /// <param name="schedule">The units, the one to start first leading.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="output">The output; what a unit writes appears together, once the unit is done.</param>
    /// <param name="stage">The stage to record each project's time in.</param>
    /// <returns>Zero when every unit built, otherwise the exit code of the first unit that failed.</returns>
    public static int Run(
        IToolchain toolchain,
        PipelineOptions options,
        IReadOnlyList<PlanEntry> entries,
        BuildSchedule schedule,
        string configuration,
        PipelineOutput output,
        StageRecorder stage)
    {
        var byProject = entries.ToDictionary(entry => new ProjectId(entry.ProjectPath));
        var gate = new Lock();
        var taken = -1;
        var exitCode = 0;

        Parallel.For(0, schedule.Slots, new ParallelOptions { MaxDegreeOfParallelism = schedule.Slots }, slotIndex =>
        {
            int index;
            while ((index = Interlocked.Increment(ref taken)) < schedule.Units.Count)
            {
                var unit = schedule.Units[index].Unit;
                var slot = new BuildSlot($"unit-{index + 1}", schedule.CoresPerSlot);

                using var unitOut = new StringWriter();
                using var unitError = new StringWriter();
                var unitOutput = new PipelineOutput(unitOut, unitError, output.GitHubActions);

                var startTime = DateTimeOffset.UtcNow;
                var started = Stopwatch.GetTimestamp();
                var unitExitCode = toolchain.Build(options, [.. unit.Projects.Select(project => byProject[project])], configuration, slot, unitOutput, stage);
                var elapsed = Stopwatch.GetElapsedTime(started);
                var seconds = elapsed.TotalSeconds;

                stage.AddUnit(new UnitInfo
                {
                    Unit = index + 1,
                    Slot = slotIndex + 1,
                    Slots = schedule.Slots,
                    CoresPerSlot = schedule.CoresPerSlot,
                    Projects = unit.Projects.Count,
                    FirstProject = unit.Projects[0].Value,
                    ExpectedWorkMS = (long)unit.Work.TotalMilliseconds,
                    ExpectedCriticalPathMS = (long)unit.CriticalPath.TotalMilliseconds,
                    StartTime = startTime,
                    DurationMS = (long)elapsed.TotalMilliseconds,
                    Succeeded = unitExitCode == 0,
                    ExitCode = unitExitCode,
                });

                lock (gate)
                {
                    output.Out.Write(unitOut.ToString());
                    output.Error.Write(unitError.ToString());
                    output.Adopt(unitOutput);
                    output.WriteLine(string.Create(
                        CultureInfo.InvariantCulture,
                        $"--- unit {index + 1}: {(unitExitCode == 0 ? "built" : "failed")} {unit.Projects.Count} project(s) in {seconds:0.0} s"));

                    if (exitCode == 0)
                        exitCode = unitExitCode;
                }
            }
        });

        return exitCode;
    }
}
