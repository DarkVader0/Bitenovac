namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Provides a method for deciding whether work set for a time of day is due.
/// </summary>
internal static class DailySchedule
{
    /// <summary>
    /// Determines whether daily work set for <paramref name="at"/> is due.
    /// </summary>
    /// <param name="at">The time of day, in UTC, the work is set for.</param>
    /// <param name="last">The day it last ran.</param>
    /// <param name="now">The current time.</param>
    /// <returns><see langword="true"/> once the time has passed on a day it has not run.</returns>
    public static bool IsDue(TimeOnly at, DateOnly? last, DateTimeOffset now) =>
        last != DateOnly.FromDateTime(now.UtcDateTime) && TimeOnly.FromDateTime(now.UtcDateTime) >= at;
}
