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
/// Each unit is a build of its own. Whenever a slot is free it takes the first unit, in the
/// schedule's order, whose dependencies have all been built; the order puts the longest chain first.
/// </para>
/// <para>
/// A unit that fails does not stop the units that do not depend on it: they share nothing with
/// it, and their failures, if any, are worth knowing in the same run. A unit that does depend on
/// it is skipped, since what it needs is not there.
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
        var units = schedule.Units.Select(scheduled => scheduled.Unit).ToList();

        var pending = Enumerable.Range(0, units.Count).ToList();
        var built = new Dictionary<BuildUnit, bool>();
        var freeSlots = new Stack<int>(Enumerable.Range(1, schedule.Slots).Reverse());
        var running = new List<Task<UnitRun>>();
        var exitCode = 0;

        while (true)
        {
            // A unit behind one that failed, or was itself skipped, has nothing to build on.
            while (pending.FindIndex(index => units[index].Dependencies.Any(dependency => built.TryGetValue(dependency, out var succeeded) && !succeeded)) is var blocked and >= 0)
            {
                var index = pending[blocked];
                pending.RemoveAt(blocked);
                built[units[index]] = false;
                output.WriteLine($"--- unit {index + 1}: skipped {units[index].Projects.Count} project(s), as a unit it depends on did not build");
            }

            while (freeSlots.Count > 0 && pending.FindIndex(index => units[index].Dependencies.All(built.ContainsKey)) is var ready and >= 0)
            {
                var index = pending[ready];
                pending.RemoveAt(ready);
                var slot = freeSlots.Pop();
                running.Add(Task.Run(() => Build(index, slot)));
            }

            if (running.Count == 0)
                break;

            var finished = running[Task.WaitAny([.. running])];
            running.Remove(finished);
            var run = finished.GetAwaiter().GetResult();
            var unit = units[run.Index];

            freeSlots.Push(run.Slot);
            built[unit] = run.ExitCode == 0;
            if (exitCode == 0)
                exitCode = run.ExitCode;

            stage.AddUnit(new UnitInfo
            {
                Unit = run.Index + 1,
                Slot = run.Slot,
                Slots = schedule.Slots,
                CoresPerSlot = schedule.CoresPerSlot,
                Projects = unit.Projects.Count,
                FirstProject = unit.Projects[0].Value,
                ExpectedWorkMS = (long)unit.Work.TotalMilliseconds,
                ExpectedCriticalPathMS = (long)unit.CriticalPath.TotalMilliseconds,
                StartTime = run.StartTime,
                DurationMS = (long)run.Elapsed.TotalMilliseconds,
                Succeeded = run.ExitCode == 0,
                ExitCode = run.ExitCode,
            });

            output.Out.Write(run.Out);
            output.Error.Write(run.Error);
            output.Adopt(run.Output);
            output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"--- unit {run.Index + 1}: {(run.ExitCode == 0 ? "built" : "failed")} {unit.Projects.Count} project(s) in {run.Elapsed.TotalSeconds:0.0} s"));
        }

        return exitCode;

        // Runs beside the other units, so what it reports is held back until it is done.
        UnitRun Build(int index, int slot)
        {
            using var unitOut = new StringWriter();
            using var unitError = new StringWriter();
            var unitOutput = new PipelineOutput(unitOut, unitError, output.GitHubActions);

            var startTime = DateTimeOffset.UtcNow;
            var started = Stopwatch.GetTimestamp();
            var unitExitCode = toolchain.Build(
                options,
                [.. units[index].Projects.Select(project => byProject[project])],
                configuration,
                new BuildSlot($"unit-{index + 1}", schedule.CoresPerSlot),
                unitOutput,
                stage);

            return new UnitRun(index, slot, unitExitCode, startTime, Stopwatch.GetElapsedTime(started), unitOut.ToString(), unitError.ToString(), unitOutput);
        }
    }

    private sealed record UnitRun(int Index, int Slot, int ExitCode, DateTimeOffset StartTime, TimeSpan Elapsed, string Out, string Error, PipelineOutput Output);
}
