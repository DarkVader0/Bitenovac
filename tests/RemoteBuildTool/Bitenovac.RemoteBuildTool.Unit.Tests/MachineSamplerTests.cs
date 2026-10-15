using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class MachineSamplerTests
{
    [Fact]
    public void SampleNow_ShouldAddASample_WhileTheSamplerRuns()
    {
        // Arrange
        var sampler = new MachineSampler(TestFactory.Identity(), "plan", Timeout.InfiniteTimeSpan, () => TestFactory.Counters());

        // Act
        sampler.SampleNow();

        // Assert
        Assert.Equal(3, sampler.Stop().Samples.Count);
    }

    [Fact]
    public void SampleNow_ShouldReadNothing_WhenTheSamplerHasStopped()
    {
        // Arrange
        var reads = 0;
        var sampler = new MachineSampler(TestFactory.Identity(), "plan", Timeout.InfiniteTimeSpan, () =>
        {
            reads++;
            return TestFactory.Counters();
        });
        sampler.Stop();

        // Act
        sampler.SampleNow();

        // Assert
        Assert.Equal(2, reads);
    }

    [Theory]
    [InlineData(null, 6L << 30)]
    [InlineData(0L, 0L)]
    [InlineData(8L << 30, null)]
    public void Stop_ShouldLeaveTheHostMemoryShareOut_WhenTheHostDoesNotTellIt(long? total, long? available)
    {
        // Arrange
        var counters = TestFactory.Counters() with { HostMemoryTotalBytes = total, HostMemoryAvailableBytes = available };
        var sampler = new MachineSampler(TestFactory.Identity(), "plan", Timeout.InfiniteTimeSpan, () => counters);

        // Act
        var (_, samples) = sampler.Stop();

        // Assert
        Assert.All(samples, sample => Assert.Null(sample.HostMemoryUtilization));
    }
}
