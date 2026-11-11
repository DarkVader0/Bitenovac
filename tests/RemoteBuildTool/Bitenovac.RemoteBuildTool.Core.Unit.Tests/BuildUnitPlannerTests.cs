using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Core.Scheduling;

namespace Bitenovac.RemoteBuildTool.Core.Unit.Tests;

public sealed class BuildUnitPlannerTests
{
    private static readonly TimeSpan Default = TimeSpan.FromSeconds(1);

    [Fact]
    public void Plan_ShouldThrow_WhenTheGraphIsNull()
    {
        // Arrange
        var toBuild = TestFactory.Projects();

        // Act
        var act = () => BuildUnitPlanner.Plan(null!, toBuild, TestFactory.Durations(), Default);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Plan_ShouldThrow_WhenTheProjectsToBuildAreNull()
    {
        // Arrange
        var graph = TestFactory.Graph("A");

        // Act
        var act = () => BuildUnitPlanner.Plan(graph, null!, TestFactory.Durations(), Default);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Plan_ShouldThrow_WhenTheDurationsAreNull()
    {
        // Arrange
        var graph = TestFactory.Graph("A");

        // Act
        var act = () => BuildUnitPlanner.Plan(graph, TestFactory.Projects("A"), null!, Default);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Plan_ShouldThrow_WhenAProjectToBuildIsNotInTheGraph()
    {
        // Arrange
        var graph = TestFactory.Graph("A");

        // Act
        var act = () => BuildUnitPlanner.Plan(graph, TestFactory.Projects("A", "Unknown"), TestFactory.Durations(), Default);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Plan_ShouldThrow_WhenADurationIsNegative()
    {
        // Arrange
        var graph = TestFactory.Graph("A");

        // Act
        var act = () => BuildUnitPlanner.Plan(graph, TestFactory.Projects("A"), TestFactory.Durations(("A", -1)), Default);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Plan_ShouldThrow_WhenAskedToSplitIntoNoUnits()
    {
        // Arrange
        var graph = TestFactory.Graph("A");

        // Act
        var act = () => BuildUnitPlanner.Plan(graph, TestFactory.Projects("A"), TestFactory.Durations(), Default, splitInto: 0);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void Plan_ShouldBuildWhatSeveralTopProjectsShareFirst_WhenAskedToSplit()
    {
        // Arrange
        var graph = TestFactory.Graph("App1 -> Lib", "App2 -> Lib");
        var durations = TestFactory.Durations(("Lib", 4), ("App1", 3), ("App2", 2));

        // Act
        var units = BuildUnitPlanner.Plan(graph, TestFactory.Projects("Lib", "App1", "App2"), durations, Default, splitInto: 3);

        // Assert
        // Lib is needed by both apps, so it is a unit of its own with the longer app, 3, still to
        // come after its 4. Each app is a unit that waits for it.
        Assert.Equal(
            [(["Lib"], 4, 4, 7), (["App1"], 3, 3, 3), (["App2"], 2, 2, 2)],
            units.Select(Summary));
        Assert.Empty(units[0].Dependencies);
        Assert.Equal([units[0]], units[1].Dependencies);
        Assert.Equal([units[0]], units[2].Dependencies);
    }

    [Fact]
    public void Plan_ShouldKeepWhatOnlyOneTopProjectNeedsWithIt_WhenAskedToSplit()
    {
        // Arrange
        var graph = TestFactory.Graph("App1 -> Own1, Own2, Lib", "Own1 -> Deep", "Own2 -> Deep", "App2 -> Lib");

        // Act
        var units = BuildUnitPlanner.Plan(graph, TestFactory.Projects("App1", "App2", "Own1", "Own2", "Deep", "Lib"), TestFactory.Durations(), Default, splitInto: 2);

        // Assert
        // Only App1 needs Own1, Own2 and Deep, by two routes, so they stay with it: 4 of work, and
        // the chain Deep, Own, App1 of 3. Lib is shared: 1, with that chain of 3 still to come.
        Assert.Equal(
            [(["Lib"], 1, 1, 4), (["Deep", "Own1", "Own2", "App1"], 4, 3, 3), (["App2"], 1, 1, 1)],
            units.Select(Summary));
    }

    [Fact]
    public void Plan_ShouldSpreadTheTopProjectsOverTheUnitsByWork_WhenThereAreMoreOfThemThanUnits()
    {
        // Arrange
        var graph = TestFactory.Graph("A -> Lib", "B -> Lib", "C -> Lib");
        var durations = TestFactory.Durations(("A", 5), ("B", 3), ("C", 2), ("Lib", 1));

        // Act
        var units = BuildUnitPlanner.Plan(graph, TestFactory.Projects("A", "B", "C", "Lib"), durations, Default, splitInto: 2);

        // Assert
        // Longest first into the unit with the least work: A, 5, into the first; B, 3, into the
        // second; C, 2, into the second again, which then holds 5 of work and a longest chain of 3.
        Assert.Equal(
            [(["Lib"], 1, 1, 6), (["A"], 5, 5, 5), (["B", "C"], 5, 3, 3)],
            units.Select(Summary));
    }

    [Fact]
    public void Plan_ShouldLeaveAGroupWhole_WhenOnlyOneProjectSitsOnTopOfIt()
    {
        // Arrange
        var graph = TestFactory.Graph("C -> B", "B -> A");

        // Act
        var units = BuildUnitPlanner.Plan(graph, TestFactory.Projects("A", "B", "C"), TestFactory.Durations(), Default, splitInto: 4);

        // Assert
        var unit = Assert.Single(units);
        Assert.Equal([TestFactory.Id("A"), TestFactory.Id("B"), TestFactory.Id("C")], unit.Projects);
        Assert.Empty(unit.Dependencies);
    }

    [Fact]
    public void Plan_ShouldReturnNoUnits_WhenThereIsNothingToBuild()
    {
        // Arrange
        var graph = TestFactory.Graph("B -> A");

        // Act
        var units = BuildUnitPlanner.Plan(graph, TestFactory.Projects(), TestFactory.Durations(), Default);

        // Assert
        Assert.Empty(units);
    }

    [Fact]
    public void Plan_ShouldPutProjectsThatReferenceEachOtherInOneUnit_WhenAllOfThemAreBuilt()
    {
        // Arrange
        var graph = TestFactory.Graph("C -> B", "B -> A");
        var durations = TestFactory.Durations(("A", 1), ("B", 2), ("C", 3));

        // Act
        var units = BuildUnitPlanner.Plan(graph, TestFactory.Projects("A", "B", "C"), durations, Default);

        // Assert
        // One chain: the work and the critical path are both 1 + 2 + 3.
        var unit = Assert.Single(units);
        Assert.Equal([TestFactory.Id("A"), TestFactory.Id("B"), TestFactory.Id("C")], unit.Projects);
        Assert.Equal(TimeSpan.FromSeconds(6), unit.Work);
        Assert.Equal(TimeSpan.FromSeconds(6), unit.CriticalPath);
    }

    [Fact]
    public void Plan_ShouldTakeTheLongestChainAsTheCriticalPath_WhenAUnitBranches()
    {
        // Arrange
        var graph = TestFactory.Graph("D -> B, C", "B -> A", "C -> A");
        var durations = TestFactory.Durations(("A", 1), ("B", 5), ("C", 2), ("D", 1));

        // Act
        var units = BuildUnitPlanner.Plan(graph, TestFactory.Projects("A", "B", "C", "D"), durations, Default);

        // Assert
        // Work is 1 + 5 + 2 + 1. The chains are A, B, D (1 + 5 + 1) and A, C, D (1 + 2 + 1).
        var unit = Assert.Single(units);
        Assert.Equal(TimeSpan.FromSeconds(9), unit.Work);
        Assert.Equal(TimeSpan.FromSeconds(7), unit.CriticalPath);
    }

    [Fact]
    public void Plan_ShouldKeepProjectsApart_WhenOnlyAProjectThatIsNotBuiltConnectsThem()
    {
        // Arrange
        var graph = TestFactory.Graph("Left -> Shared", "Right -> Shared");

        // Act
        var units = BuildUnitPlanner.Plan(graph, TestFactory.Projects("Left", "Right"), TestFactory.Durations(), Default);

        // Assert
        Assert.Equal([[TestFactory.Id("Left")], [TestFactory.Id("Right")]], units.Select(unit => unit.Projects));
    }

    [Fact]
    public void Plan_ShouldStartWithTheLongestCriticalPath_WhenAnotherUnitHasMoreWork()
    {
        // Arrange
        var graph = TestFactory.Graph("Chain2 -> Chain1", "Wide -> Leaf1, Leaf2, Leaf3");
        var durations = TestFactory.Durations(("Chain1", 3), ("Chain2", 3), ("Wide", 1), ("Leaf1", 2), ("Leaf2", 2), ("Leaf3", 2));

        // Act
        var units = BuildUnitPlanner.Plan(graph, TestFactory.Projects("Chain1", "Chain2", "Wide", "Leaf1", "Leaf2", "Leaf3"), durations, Default);

        // Assert
        // The chain: work 3 + 3, critical path 3 + 3. The wide unit: work 1 + 2 + 2 + 2, critical path 2 + 1.
        Assert.Equal(
            [(TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(6)), (TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(3))],
            units.Select(unit => (unit.Work, unit.CriticalPath)));
    }

    [Fact]
    public void Plan_ShouldStartWithTheLargerUnit_WhenCriticalPathsAreEqual()
    {
        // Arrange
        var graph = TestFactory.Graph("Single", "Top -> Below1, Below2");
        var durations = TestFactory.Durations(("Single", 4), ("Top", 2), ("Below1", 2), ("Below2", 2));

        // Act
        var units = BuildUnitPlanner.Plan(graph, TestFactory.Projects("Single", "Top", "Below1", "Below2"), durations, Default);

        // Assert
        // Both critical paths are 4: Single alone, and Below, Top (2 + 2). Work is 4 against 2 + 2 + 2.
        Assert.Equal([TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(4)], units.Select(unit => unit.Work));
    }

    [Fact]
    public void Plan_ShouldOrderUnitsByPath_WhenTheyAreOtherwiseEqual()
    {
        // Arrange
        var graph = TestFactory.Graph("b", "a", "c");

        // Act
        var units = BuildUnitPlanner.Plan(graph, TestFactory.Projects("c", "a", "b"), TestFactory.Durations(), Default);

        // Assert
        Assert.Equal([TestFactory.Id("a"), TestFactory.Id("b"), TestFactory.Id("c")], units.Select(unit => unit.Projects.Single()));
    }

    [Fact]
    public void Plan_ShouldUseTheDefaultDuration_WhenAProjectHasNone()
    {
        // Arrange
        var graph = TestFactory.Graph("B -> A");
        var durations = TestFactory.Durations(("A", 4));

        // Act
        var units = BuildUnitPlanner.Plan(graph, TestFactory.Projects("A", "B"), durations, TimeSpan.FromSeconds(7));

        // Assert
        // A is known to take 4; B has no duration and takes the default, 7.
        var unit = Assert.Single(units);
        Assert.Equal(TimeSpan.FromSeconds(11), unit.Work);
        Assert.Equal(TimeSpan.FromSeconds(11), unit.CriticalPath);
    }

    private static (string[] Projects, int Work, int CriticalPath, int RemainingPath) Summary(BuildUnit unit) =>
        ([.. unit.Projects.Select(project => project.Value)], (int)unit.Work.TotalSeconds, (int)unit.CriticalPath.TotalSeconds, (int)unit.RemainingPath.TotalSeconds);
}
