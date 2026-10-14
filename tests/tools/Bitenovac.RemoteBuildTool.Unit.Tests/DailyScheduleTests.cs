using System.Globalization;
using Bitenovac.RemoteBuildTool.Service;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class DailyScheduleTests
{
    [Theory]
    [InlineData("03:30", null, "2026-10-14T03:29:59Z", false)]
    [InlineData("03:30", null, "2026-10-14T03:30:00Z", true)]
    [InlineData("03:30", "2026-10-13", "2026-10-14T23:00:00Z", true)]
    [InlineData("03:30", "2026-10-14", "2026-10-14T23:00:00Z", false)]
    public void IsDue_ShouldBeTrueOncePerDayAfterItsTime_WhenAskedAtAnyTime(string at, string? last, string now, bool expected)
    {
        // Arrange
        var time = TimeOnly.Parse(at, CultureInfo.InvariantCulture);
        DateOnly? lastRun = last is null ? null : DateOnly.Parse(last, CultureInfo.InvariantCulture);

        // Act
        var due = DailySchedule.IsDue(time, lastRun, DateTimeOffset.Parse(now, CultureInfo.InvariantCulture));

        // Assert
        Assert.Equal(expected, due);
    }
}
