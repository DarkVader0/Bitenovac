using Bitenovac.RemoteBuildTool.Core.Processes;

namespace Bitenovac.RemoteBuildTool.Core.Unit.Tests;

public sealed class ResourceMonitorTests
{
    private static readonly TimeSpan Never = TimeSpan.FromHours(1);

    [Fact]
    public void Stop_ShouldReportThePeakMemoryAndTheLatestCpuTime_WhenSamplesWereTaken()
    {
        // Arrange
        var samples = new FakeSampler(Sample(100, 1), Sample(300, 2), Sample(200, 3));
        using var monitor = new ResourceMonitor(samples.Next, Never);
        monitor.Take();
        monitor.Take();

        // Act
        var usage = monitor.Stop();

        // Assert
        Assert.Equal(300, usage.PeakMemoryBytes);
        Assert.Equal(TimeSpan.FromSeconds(3), usage.CpuTime);
    }

    [Fact]
    public void Stop_ShouldKeepTheHighestCpuTime_WhenTheTreeLosesAProcess()
    {
        // Arrange
        var samples = new FakeSampler(Sample(100, 5), Sample(100, 2));
        using var monitor = new ResourceMonitor(samples.Next, Never);
        monitor.Take();

        // Act
        var usage = monitor.Stop();

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(5), usage.CpuTime);
    }

    [Fact]
    public void Stop_ShouldReportOnlyTheElapsedTime_WhenNoSampleCouldBeRead()
    {
        // Arrange
        var samples = new FakeSampler(null, null);
        using var monitor = new ResourceMonitor(samples.Next, Never);
        monitor.Take();

        // Act
        var usage = monitor.Stop();

        // Assert
        Assert.Null(usage.CpuTime);
        Assert.Null(usage.PeakMemoryBytes);
        Assert.True(usage.Elapsed >= TimeSpan.Zero);
    }

    [Fact]
    public void Stop_ShouldReturnTheFirstUsage_WhenCalledAgainAfterLaterSamples()
    {
        // Arrange
        var samples = new FakeSampler(Sample(100, 1), Sample(900, 9));
        using var monitor = new ResourceMonitor(samples.Next, Never);
        var first = monitor.Stop();
        monitor.Take();

        // Act
        var second = monitor.Stop();

        // Assert
        Assert.Same(first, second);
        Assert.Equal(100, second.PeakMemoryBytes);
    }

    [Fact]
    public async Task Constructor_ShouldSampleAgain_WhenTheIntervalPasses()
    {
        // Arrange
        var sampledTwice = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;

        // Act
        using var monitor = new ResourceMonitor(
            () =>
            {
                if (Interlocked.Increment(ref calls) == 2)
                    sampledTwice.SetResult();
                return Sample(100, 1);
            },
            TimeSpan.FromMilliseconds(1));
        await sampledTwice.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(Volatile.Read(ref calls) >= 2);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTheSamplerIsNull()
    {
        // Arrange
        Func<ProcessTreeSample?> sample = null!;

        // Act
        Action act = () => new ResourceMonitor(sample, Never);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTheIntervalIsNotPositive()
    {
        // Arrange
        var samples = new FakeSampler();

        // Act
        Action act = () => new ResourceMonitor(samples.Next, TimeSpan.Zero);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    private static ProcessTreeSample Sample(long residentBytes, int cpuSeconds) =>
        new(residentBytes, TimeSpan.FromSeconds(cpuSeconds), ProcessCount: 1);

    private sealed class FakeSampler(params ProcessTreeSample?[] samples)
    {
        private int _next;

        public ProcessTreeSample? Next() => _next < samples.Length ? samples[_next++] : null;
    }
}
