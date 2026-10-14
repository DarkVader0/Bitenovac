using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class MachineCountersTests
{
    private const int Precision = 6;

    [Fact]
    public void Read_ShouldReadTheHostAndTheContainer_WhenBothAreExposed()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestFactory.WriteFile(directory.Combine("proc/stat"), "cpu  100 20 30 400 50 6 7 8 0 0\ncpu0 1 2 3 4\n");
        TestFactory.WriteFile(directory.Combine("proc/meminfo"), "MemTotal:       8000 kB\nMemFree:        1000 kB\nMemAvailable:   6000 kB\n");
        TestFactory.WriteFile(directory.Combine("cgroup/cpu.max"), "150000 100000\n");
        TestFactory.WriteFile(directory.Combine("cgroup/cpu.stat"), "usage_usec 2500000\nuser_usec 2000000\n");
        TestFactory.WriteFile(directory.Combine("cgroup/memory.current"), "1048576\n");
        TestFactory.WriteFile(directory.Combine("cgroup/memory.peak"), "2097152\n");
        TestFactory.WriteFile(directory.Combine("cgroup/memory.max"), "4194304\n");

        // Act
        var counters = MachineCounters.Read(directory.Combine("proc"), directory.Combine("cgroup"));

        // Assert
        Assert.Equal(100 + 20 + 30 + 6 + 7 + 8, counters.HostCpuBusyTicks);
        Assert.Equal(100 + 20 + 30 + 400 + 50 + 6 + 7 + 8, counters.HostCpuTotalTicks);
        Assert.Equal(8000 * 1024, counters.HostMemoryTotalBytes);
        Assert.Equal(6000 * 1024, counters.HostMemoryAvailableBytes);
        Assert.Equal(2000 * 1024, counters.HostMemoryUsedBytes);
        Assert.Equal(TimeSpan.FromSeconds(2.5), counters.ContainerCpuTime);
        Assert.Equal(1048576, counters.ContainerMemoryBytes);
        Assert.Equal(2097152, counters.ContainerPeakMemoryBytes);
        Assert.Equal(4194304, counters.ContainerMemoryLimitBytes);
        Assert.Equal(1.5, counters.ContainerCpuLimit!.Value, Precision);
    }

    [Fact]
    public void Read_ShouldLeaveTheLimitsOut_WhenTheContainerHasNone()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestFactory.WriteFile(directory.Combine("cgroup/cpu.max"), "max 100000\n");
        TestFactory.WriteFile(directory.Combine("cgroup/memory.max"), "max\n");

        // Act
        var counters = MachineCounters.Read(directory.Combine("proc"), directory.Combine("cgroup"));

        // Assert
        Assert.Null(counters.ContainerCpuLimit);
        Assert.Null(counters.ContainerMemoryLimitBytes);
        Assert.Null(counters.ContainerCpuTime);
    }

    [Fact]
    public void Read_ShouldReadNoContainer_WhenTheCgroupIsNotAContainersOwn()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestFactory.WriteFile(directory.Combine("cgroup/cpu.stat"), "usage_usec 2500000\n");
        TestFactory.WriteFile(directory.Combine("cgroup/memory.current"), "1048576\n");

        // Act
        var counters = MachineCounters.Read(directory.Combine("proc"), directory.Combine("cgroup"));

        // Assert
        Assert.Null(counters.ContainerCpuTime);
        Assert.Null(counters.ContainerMemoryBytes);
        Assert.Null(counters.HostCpuTotalTicks);
        Assert.Null(counters.HostMemoryTotalBytes);
    }

    [Fact]
    public void Read_ShouldReadNoHostCpu_WhenStatHasNoTotalLine()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestFactory.WriteFile(directory.Combine("proc/stat"), "intr 1 2 3\n");
        TestFactory.WriteFile(directory.Combine("cgroup/cpu.max"), "100000\n");

        // Act
        var counters = MachineCounters.Read(directory.Combine("proc"), directory.Combine("cgroup"));

        // Assert
        Assert.Null(counters.HostCpuBusyTicks);
        Assert.Null(counters.ContainerCpuLimit);
    }

    [Fact]
    public void HostCpuUtilizationSince_ShouldBeTheBusyShareOfTheTicksBetween_WhenTimePassed()
    {
        // Arrange
        var earlier = TestFactory.Counters(hostBusy: 100, hostTotal: 1000);
        var now = TestFactory.Counters(hostBusy: 400, hostTotal: 2000);

        // Act
        var utilization = now.HostCpuUtilizationSince(earlier);

        // Assert
        Assert.Equal((400.0 - 100) / (2000 - 1000), utilization!.Value, Precision);
    }

    [Fact]
    public void HostCpuUtilizationSince_ShouldBeNull_WhenNoTimePassed()
    {
        // Arrange
        var counters = TestFactory.Counters(hostBusy: 100, hostTotal: 1000);

        // Act
        var utilization = counters.HostCpuUtilizationSince(counters);

        // Assert
        Assert.Null(utilization);
    }
}
