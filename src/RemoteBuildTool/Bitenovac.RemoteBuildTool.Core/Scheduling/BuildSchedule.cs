namespace Bitenovac.RemoteBuildTool.Core.Scheduling;

/// <summary>
/// Represents where and when a unit is expected to be built.
/// </summary>
/// <param name="Unit">The unit.</param>
/// <param name="Slot">The slot that builds it, counted from zero.</param>
/// <param name="Start">When it starts, from the start of the build.</param>
/// <param name="End">When it ends, from the start of the build.</param>
public sealed record ScheduledUnit(BuildUnit Unit, int Slot, TimeSpan Start, TimeSpan End);

/// <summary>
/// Represents the expected course of a build that builds several units at once.
/// </summary>
public sealed class BuildSchedule
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BuildSchedule"/> class.
    /// </summary>
    /// <param name="units">The units, in the order they were taken.</param>
    /// <param name="slots">How many units are built at once.</param>
    /// <param name="coresPerSlot">How many cores each slot builds with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="units"/> is <see langword="null"/>.</exception>
    public BuildSchedule(IEnumerable<ScheduledUnit> units, int slots, int coresPerSlot)
    {
        ArgumentNullException.ThrowIfNull(units);

        Units = [.. units];
        Slots = slots;
        CoresPerSlot = coresPerSlot;
        Duration = Units.Select(unit => unit.End).DefaultIfEmpty(TimeSpan.Zero).Max();
    }

    /// <summary>
    /// Gets the units, in the order they were taken.
    /// </summary>
    public IReadOnlyList<ScheduledUnit> Units { get; }

    /// <summary>
    /// Gets how many units are built at once.
    /// </summary>
    public int Slots { get; }

    /// <summary>
    /// Gets how many cores each slot builds with.
    /// </summary>
    public int CoresPerSlot { get; }

    /// <summary>
    /// Gets how long the build is expected to take: when its last unit ends.
    /// </summary>
    public TimeSpan Duration { get; }
}
