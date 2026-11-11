namespace Bitenovac.RemoteBuildTool.Core.Scheduling;

/// <summary>
/// Provides methods for working out, before anything is built, how a build that builds several
/// units at once is expected to go.
/// </summary>
public static class BuildScheduler
{
    /// <summary>
    /// Works out how the build goes with a given number of slots.
    /// </summary>
    /// <param name="units">The units, the one to start first leading, as <see cref="BuildUnitPlanner.Plan"/> orders them.</param>
    /// <param name="slots">How many units are built at once.</param>
    /// <param name="coresPerSlot">How many cores each slot builds with.</param>
    /// <returns>
    /// <para>
    /// The schedule in which each unit, in order, goes to the slot that is free first, the lowest
    /// numbered one among equals.
    /// </para>
    /// <para>
    /// A unit is expected to take the longer of its <see cref="BuildUnit.CriticalPath"/> and its
    /// <see cref="BuildUnit.Work"/> divided by <paramref name="coresPerSlot"/>: no number of cores
    /// shortens its longest chain, and no ordering does its work with fewer core-seconds.
    /// </para>
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="units"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="slots"/> or <paramref name="coresPerSlot"/> is less than one.</exception>
    public static BuildSchedule Schedule(IReadOnlyList<BuildUnit> units, int slots, int coresPerSlot)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentOutOfRangeException.ThrowIfLessThan(slots, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(coresPerSlot, 1);

        var free = new TimeSpan[slots];
        var scheduled = new List<ScheduledUnit>(units.Count);
        foreach (var unit in units)
        {
            var slot = Array.IndexOf(free, free.Min());
            var shared = unit.Work / coresPerSlot;
            var end = free[slot] + (unit.CriticalPath > shared ? unit.CriticalPath : shared);
            scheduled.Add(new ScheduledUnit(unit, slot, free[slot], end));
            free[slot] = end;
        }

        return new BuildSchedule(scheduled, slots, coresPerSlot);
    }

    /// <summary>
    /// Works out how many units to build at once, within the limits given, and how the build then goes.
    /// </summary>
    /// <param name="units">The units, the one to start first leading, as <see cref="BuildUnitPlanner.Plan"/> orders them.</param>
    /// <param name="minimumSlots">The fewest units to build at once.</param>
    /// <param name="maximumSlots">The most units to build at once.</param>
    /// <param name="cores">The cores the build has, shared equally among its slots.</param>
    /// <returns>
    /// The schedule with the shortest <see cref="BuildSchedule.Duration"/> among those of every number
    /// of slots allowed, and the one with the fewest slots among equals, since every slot is a
    /// build to start. More slots than <paramref name="cores"/> or than units are never tried,
    /// unless <paramref name="minimumSlots"/> asks for them.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="units"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="minimumSlots"/> or <paramref name="cores"/> is less than one, or
    /// <paramref name="maximumSlots"/> is less than <paramref name="minimumSlots"/>.
    /// </exception>
    public static BuildSchedule Best(IReadOnlyList<BuildUnit> units, int minimumSlots, int maximumSlots, int cores)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumSlots, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumSlots, minimumSlots);
        ArgumentOutOfRangeException.ThrowIfLessThan(cores, 1);

        var most = Math.Max(minimumSlots, Math.Min(maximumSlots, Math.Min(cores, units.Count)));
        return Enumerable.Range(minimumSlots, most - minimumSlots + 1)
            .Select(slots => Schedule(units, slots, Math.Max(1, cores / slots)))
            .MinBy(schedule => schedule.Duration)!;
    }
}
