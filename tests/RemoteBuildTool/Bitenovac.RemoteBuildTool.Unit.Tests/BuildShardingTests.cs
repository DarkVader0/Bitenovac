using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Core.Planning;
using Bitenovac.RemoteBuildTool.Planning;
using Bitenovac.RemoteBuildTool.Storage;

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
    public void Schedule_ShouldExpectTheAverageOfTheKnownTimes_WhenAProjectHasNeverBeenBuilt()
    {
        // Arrange
        PlanEntry[] toBuild =
        [
            TestFactory.Entry("src/Known1/Known1.csproj"),
            TestFactory.Entry("src/Known2/Known2.csproj"),
            TestFactory.Entry("src/New/New.csproj"),
        ];
        var durations = new Dictionary<ProjectId, TimeSpan>
        {
            [TestFactory.Id("src/Known1/Known1.csproj")] = TimeSpan.FromSeconds(2),
            [TestFactory.Id("src/Known2/Known2.csproj")] = TimeSpan.FromSeconds(6),
        };

        // Act
        var schedule = BuildSharding.Schedule(toBuild, SlotLimits.One, cores: 1, durations);

        // Assert
        // The new project is expected to take (2 + 6) / 2 = 4, which puts it between the two.
        Assert.Equal(
            [("src/Known2/Known2.csproj", TimeSpan.FromSeconds(6)), ("src/New/New.csproj", TimeSpan.FromSeconds(4)), ("src/Known1/Known1.csproj", TimeSpan.FromSeconds(2))],
            schedule.Units.Select(unit => (unit.Unit.Projects.Single().Value, unit.Unit.Work)));
    }

    [Fact]
    public void Durations_ShouldReturnTheTimeOfEachProjectMainHoldsOneFor_WhenSomeWereNeverBuilt()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var main = new LocalVolumeArtifactStore(directory.Combine("main"));
        TestFactory.WriteFile(directory.Combine("built"), BuildDuration.Path, "1500");
        main.Put(TestFactory.Id("src/Built/Built.csproj"), "Debug", directory.Combine("built"), new StoredTargetHash("own", "full"));
        TestFactory.WriteFile(directory.Combine("unreadable"), BuildDuration.Path, "soon");
        main.Put(TestFactory.Id("src/Unreadable/Unreadable.csproj"), "Debug", directory.Combine("unreadable"), new StoredTargetHash("own", "full"));
        PlanEntry[] toBuild =
        [
            TestFactory.Entry("src/Built/Built.csproj"),
            TestFactory.Entry("src/Unreadable/Unreadable.csproj"),
            TestFactory.Entry("src/New/New.csproj"),
        ];

        // Act
        var durations = BuildSharding.Durations(toBuild, main, "Debug");

        // Assert
        var known = Assert.Single(durations);
        Assert.Equal((TestFactory.Id("src/Built/Built.csproj"), TimeSpan.FromMilliseconds(1500)), (known.Key, known.Value));
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
