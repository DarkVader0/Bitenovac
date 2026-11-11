using Bitenovac.RemoteBuildTool.Planning;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class BuildShardingTests
{
    [Fact]
    public void Schedule_ShouldBuildIndependentProjectsSideBySide_WhenTheLimitsAllowIt()
    {
        // Arrange
        PlanEntry[] toBuild =
        [
            TestFactory.Entry("src/App/App.csproj", references: ["src/Lib/Lib.csproj"]),
            TestFactory.Entry("src/Lib/Lib.csproj"),
            TestFactory.Entry("src/Other/Other.csproj"),
        ];

        // Act
        var schedule = BuildSharding.Schedule(toBuild, new SlotLimits(1, 4), cores: 4);

        // Assert
        // Lib and App form a chain of two; Other stands alone. Two units, so two slots of two cores.
        Assert.Equal((2, 2), (schedule.Slots, schedule.CoresPerSlot));
        Assert.Equal(
            [["src/Lib/Lib.csproj", "src/App/App.csproj"], ["src/Other/Other.csproj"]],
            schedule.Units.Select(unit => unit.Unit.Projects.Select(project => project.Value)));
    }

    [Fact]
    public void Schedule_ShouldKeepProjectsApart_WhenTheySharedOnlyAProjectThatIsNotBuilt()
    {
        // Arrange
        PlanEntry[] toBuild =
        [
            TestFactory.Entry("src/Left/Left.csproj", references: ["src/Restored/Restored.csproj"]),
            TestFactory.Entry("src/Right/Right.csproj", references: ["src/Restored/Restored.csproj"]),
        ];

        // Act
        var schedule = BuildSharding.Schedule(toBuild, SlotLimits.One, cores: 8);

        // Assert
        Assert.Equal(1, schedule.Slots);
        Assert.Equal(
            [["src/Left/Left.csproj"], ["src/Right/Right.csproj"]],
            schedule.Units.Select(unit => unit.Unit.Projects.Select(project => project.Value)));
    }

    [Fact]
    public void Describe_ShouldNameEachUnitsSlotSizeAndFirstProject_WhenAScheduleHasSeveral()
    {
        // Arrange
        PlanEntry[] toBuild =
        [
            TestFactory.Entry("src/App/App.csproj", references: ["src/Lib/Lib.csproj"]),
            TestFactory.Entry("src/Lib/Lib.csproj"),
            TestFactory.Entry("src/Other/Other.csproj"),
        ];
        var schedule = BuildSharding.Schedule(toBuild, new SlotLimits(2, 2), cores: 2);

        // Act
        var lines = BuildSharding.Describe(schedule);

        // Assert
        Assert.Equal(
            $"  unit 1 (slot 1): 2 project(s), from src/Lib/Lib.csproj{Environment.NewLine}"
            + $"  unit 2 (slot 2): 1 project(s), from src/Other/Other.csproj{Environment.NewLine}",
            lines);
    }
}
