using Bitenovac.RemoteBuildTool.Core.Scheduling;

namespace Bitenovac.RemoteBuildTool.Core.Unit.Tests;

public sealed class BuildSchedulerTests
{
    [Fact]
    public void Schedule_ShouldThrow_WhenTheUnitsAreNull()
    {
        // Arrange

        // Act
        var act = () => BuildScheduler.Schedule(null!, slots: 1, coresPerSlot: 1);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void Schedule_ShouldThrow_WhenThereIsNoSlotOrNoCore(int slots, int coresPerSlot)
    {
        // Arrange
        BuildUnit[] units = [TestFactory.Unit("A", work: 1, criticalPath: 1)];

        // Act
        var act = () => BuildScheduler.Schedule(units, slots, coresPerSlot);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void Schedule_ShouldTakeNoTime_WhenThereAreNoUnits()
    {
        // Arrange

        // Act
        var schedule = BuildScheduler.Schedule([], slots: 2, coresPerSlot: 4);

        // Assert
        Assert.Empty(schedule.Units);
        Assert.Equal(TimeSpan.Zero, schedule.Duration);
        Assert.Equal((2, 4), (schedule.Slots, schedule.CoresPerSlot));
    }

    [Theory]
    [InlineData(2, 6)]
    [InlineData(4, 4)]
    public void Schedule_ShouldTakeTheLongerOfTheCriticalPathAndTheSharedWork_WhenAUnitIsBuilt(int coresPerSlot, int expectedSeconds)
    {
        // Arrange
        // Work 12 on 2 cores is 6, longer than the critical path, 4. On 4 cores it is 3, shorter.
        BuildUnit[] units = [TestFactory.Unit("A", work: 12, criticalPath: 4)];

        // Act
        var schedule = BuildScheduler.Schedule(units, slots: 1, coresPerSlot);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), schedule.Duration);
    }

    [Fact]
    public void Schedule_ShouldGiveEachUnitTheSlotThatIsFreeFirst_WhenThereAreMoreUnitsThanSlots()
    {
        // Arrange
        BuildUnit[] units =
        [
            TestFactory.Unit("A", work: 6, criticalPath: 6),
            TestFactory.Unit("B", work: 3, criticalPath: 3),
            TestFactory.Unit("C", work: 2, criticalPath: 2),
        ];

        // Act
        var schedule = BuildScheduler.Schedule(units, slots: 2, coresPerSlot: 1);

        // Assert
        // A takes slot 0 until 6 and B slot 1 until 3. Slot 1 is free first, so C runs there from 3 to 5.
        Assert.Equal(
            [(0, TimeSpan.Zero, TimeSpan.FromSeconds(6)), (1, TimeSpan.Zero, TimeSpan.FromSeconds(3)), (1, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5))],
            schedule.Units.Select(unit => (unit.Slot, unit.Start, unit.End)));
        Assert.Equal(units, schedule.Units.Select(unit => unit.Unit));
        Assert.Equal(TimeSpan.FromSeconds(6), schedule.Duration);
    }

    [Fact]
    public void Best_ShouldThrow_WhenTheUnitsAreNull()
    {
        // Arrange

        // Act
        var act = () => BuildScheduler.Best(null!, minimumSlots: 1, maximumSlots: 1, cores: 1);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(2, 1, 1)]
    [InlineData(1, 1, 0)]
    public void Best_ShouldThrow_WhenTheLimitsAreOutOfRange(int minimumSlots, int maximumSlots, int cores)
    {
        // Arrange
        BuildUnit[] units = [TestFactory.Unit("A", work: 1, criticalPath: 1)];

        // Act
        var act = () => BuildScheduler.Best(units, minimumSlots, maximumSlots, cores);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void Best_ShouldUseASlotPerUnit_WhenTheUnitsAreChainsThatNeedOneCoreEach()
    {
        // Arrange
        BuildUnit[] units =
        [
            TestFactory.Unit("A", work: 4, criticalPath: 4),
            TestFactory.Unit("B", work: 4, criticalPath: 4),
            TestFactory.Unit("C", work: 4, criticalPath: 4),
            TestFactory.Unit("D", work: 4, criticalPath: 4),
        ];

        // Act
        var schedule = BuildScheduler.Best(units, minimumSlots: 1, maximumSlots: 4, cores: 4);

        // Assert
        // A chain takes 4 however many cores it has. One slot builds them in 16, two or three in 8, four in 4.
        Assert.Equal((4, 1), (schedule.Slots, schedule.CoresPerSlot));
        Assert.Equal(TimeSpan.FromSeconds(4), schedule.Duration);
    }

    [Fact]
    public void Best_ShouldKeepEveryCoreInOneSlot_WhenSplittingThemSlowsAWideUnit()
    {
        // Arrange
        // On 8 cores the unit takes max(2, 16 / 8) = 2. In one of two slots, with 4 cores, it takes 4.
        BuildUnit[] units = [TestFactory.Unit("Wide", work: 16, criticalPath: 2), TestFactory.Unit("Small", work: 1, criticalPath: 1)];

        // Act
        var schedule = BuildScheduler.Best(units, minimumSlots: 1, maximumSlots: 2, cores: 8);

        // Assert
        // One slot: 2, then 1, so 3. Two slots: 4 beside 1, so 4.
        Assert.Equal((1, 8), (schedule.Slots, schedule.CoresPerSlot));
        Assert.Equal(TimeSpan.FromSeconds(3), schedule.Duration);
    }

    [Fact]
    public void Best_ShouldUseTheFewestSlots_WhenMoreWouldNotBeFaster()
    {
        // Arrange
        BuildUnit[] units = [TestFactory.Unit("Chain", work: 10, criticalPath: 10)];

        // Act
        var schedule = BuildScheduler.Best(units, minimumSlots: 1, maximumSlots: 8, cores: 8);

        // Assert
        Assert.Equal(1, schedule.Slots);
        Assert.Equal(TimeSpan.FromSeconds(10), schedule.Duration);
    }

    [Fact]
    public void Best_ShouldNotTryMoreSlotsThanCores_WhenTheMaximumAllowsThem()
    {
        // Arrange
        BuildUnit[] units =
        [
            TestFactory.Unit("A", work: 4, criticalPath: 4),
            TestFactory.Unit("B", work: 4, criticalPath: 4),
            TestFactory.Unit("C", work: 4, criticalPath: 4),
        ];

        // Act
        var schedule = BuildScheduler.Best(units, minimumSlots: 1, maximumSlots: 3, cores: 2);

        // Assert
        // Three slots would build the chains in 4, but there are two cores: two slots take 4 + 4.
        Assert.Equal((2, 1), (schedule.Slots, schedule.CoresPerSlot));
        Assert.Equal(TimeSpan.FromSeconds(8), schedule.Duration);
    }

    [Fact]
    public void Best_ShouldUseTheMinimum_WhenItAsksForMoreSlotsThanThereAreUnitsOrCores()
    {
        // Arrange
        BuildUnit[] units = [TestFactory.Unit("A", work: 4, criticalPath: 4)];

        // Act
        var schedule = BuildScheduler.Best(units, minimumSlots: 3, maximumSlots: 5, cores: 2);

        // Assert
        Assert.Equal((3, 1), (schedule.Slots, schedule.CoresPerSlot));
        Assert.Equal(TimeSpan.FromSeconds(4), schedule.Duration);
    }
}
