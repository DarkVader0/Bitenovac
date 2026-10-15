using System.Globalization;

namespace Bitenovac.DecompressionCalculator.Export;

/// <summary>
/// Provides the number and time formats shown in the app and in exported plans.
/// </summary>
public static class Formats
{
    /// <summary>
    /// Returns a number with a fixed count of decimals, in the invariant culture.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <param name="decimals">The count of decimals.</param>
    /// <returns>The formatted number, such as <c>30</c> or <c>1.4</c>.</returns>
    public static string Number(double value, int decimals = 0) =>
        value.ToString($"F{decimals}", CultureInfo.InvariantCulture);

    /// <summary>
    /// Returns a duration in whole minutes, or minutes and seconds when it has seconds.
    /// </summary>
    /// <param name="duration">The duration, rounded to the second.</param>
    /// <returns>For example <c>20</c> or <c>1:40</c>.</returns>
    public static string Clock(TimeSpan duration)
    {
        var seconds = (int)Math.Round(duration.TotalSeconds, MidpointRounding.AwayFromZero);
        var minutes = (seconds / 60).ToString(CultureInfo.InvariantCulture);
        return seconds % 60 == 0 ? minutes : $"{minutes}:{(seconds % 60).ToString("00", CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Returns a duration as <see cref="Clock"/> does, followed by the minute unit.
    /// </summary>
    /// <param name="duration">The duration.</param>
    /// <returns>For example <c>20 min</c> or <c>1:40 min</c>.</returns>
    public static string Minutes(TimeSpan duration) => $"{Clock(duration)} min";

    /// <summary>
    /// Returns a duration in hours and minutes, rounded to the minute.
    /// </summary>
    /// <param name="duration">The duration.</param>
    /// <returns>For example <c>1:30</c> for ninety minutes, or <c>0:45</c>.</returns>
    public static string HoursAndMinutes(TimeSpan duration)
    {
        var minutes = (int)Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero);
        return string.Create(CultureInfo.InvariantCulture, $"{minutes / 60}:{minutes % 60:00}");
    }
}
