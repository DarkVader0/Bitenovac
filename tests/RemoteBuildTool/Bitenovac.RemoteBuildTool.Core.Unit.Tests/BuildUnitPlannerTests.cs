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
}
