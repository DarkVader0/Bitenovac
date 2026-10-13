using Bitenovac.RemoteBuildTool.Core.Processes;

namespace Bitenovac.RemoteBuildTool.Core.Unit.Tests;

public sealed class ResourceUsageTests
{
    private const int Precision = 10;
    private const long Mebibyte = 1024 * 1024;

    [Fact]
    public void AverageCores_ShouldBeCpuTimeOverElapsedTime_WhenBothWereMeasured()
    {
        // Arrange
        var usage = new ResourceUsage(TimeSpan.FromSeconds(40), TimeSpan.FromSeconds(30), PeakMemoryBytes: null);

        // Act
        var cores = usage.AverageCores;

        // Assert
        Assert.NotNull(cores);
        Assert.Equal(30.0 / 40.0, cores.Value, Precision);
    }

    [Fact]
    public void AverageCores_ShouldBeNull_WhenTheCpuTimeWasNotMeasured()
    {
        // Arrange
        var usage = new ResourceUsage(TimeSpan.FromSeconds(40), CpuTime: null, PeakMemoryBytes: null);

        // Act
        var cores = usage.AverageCores;

        // Assert
        Assert.Null(cores);
    }

    [Fact]
    public void AverageCores_ShouldBeNull_WhenNoTimePassed()
    {
        // Arrange
        var usage = new ResourceUsage(TimeSpan.Zero, TimeSpan.FromSeconds(1), PeakMemoryBytes: null);

        // Act
        var cores = usage.AverageCores;

        // Assert
        Assert.Null(cores);
    }

    [Fact]
    public void ToString_ShouldShowEveryMeasurementInMebibytes_WhenThePeakIsUnderAGibibyte()
    {
        // Arrange
        var usage = new ResourceUsage(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5), 512 * Mebibyte);

        // Act
        var text = usage.ToString();

        // Assert
        Assert.Equal("used 10.0 s, CPU 5.0 s (0.50 cores), peak memory 512 MiB", text);
    }

    [Fact]
    public void ToString_ShouldShowThePeakInGibibytes_WhenItReachesAGibibyte()
    {
        // Arrange
        var usage = new ResourceUsage(TimeSpan.FromSeconds(4), CpuTime: null, 1536 * Mebibyte);

        // Act
        var text = usage.ToString();

        // Assert
        Assert.Equal("used 4.0 s, peak memory 1.50 GiB", text);
    }

    [Fact]
    public void ToString_ShouldLeaveOutTheCores_WhenNoTimePassed()
    {
        // Arrange
        var usage = new ResourceUsage(TimeSpan.Zero, TimeSpan.FromSeconds(1), PeakMemoryBytes: null);

        // Act
        var text = usage.ToString();

        // Assert
        Assert.Equal("used 0.0 s, CPU 1.0 s", text);
    }

    [Fact]
    public void ToString_ShouldShowOnlyTheElapsedTime_WhenNothingElseWasMeasured()
    {
        // Arrange
        var usage = new ResourceUsage(TimeSpan.FromSeconds(2.5), CpuTime: null, PeakMemoryBytes: null);

        // Act
        var text = usage.ToString();

        // Assert
        Assert.Equal("used 2.5 s", text);
    }
}
