using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Core.Hashing;
using Bitenovac.RemoteBuildTool.Core.Planning;

namespace Bitenovac.RemoteBuildTool.Core.Unit.Tests;

public sealed class CachePlanBuilderTests
{
    [Fact]
    public void Build_ShouldThrow_WhenGraphIsNull()
    {
        // Arrange
        var inputs = TestFactory.OwnInputs();
        var stored = TestFactory.Stored();

        // Act
        var act = () => CachePlanBuilder.Build(null!, inputs, stored);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Build_ShouldThrow_WhenOwnHashInputsIsNull()
    {
        // Arrange
        var graph = TestFactory.Graph("A");
        var stored = TestFactory.Stored();

        // Act
        var act = () => CachePlanBuilder.Build(graph, null!, stored);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Build_ShouldThrow_WhenStoredIsNull()
    {
        // Arrange
        var graph = TestFactory.Graph("A");
        var inputs = TestFactory.OwnInputs(("A", ["a"]));

        // Act
        var act = () => CachePlanBuilder.Build(graph, inputs, null!);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Build_ShouldThrow_WhenAProjectHasNoOwnHashInputs()
    {
        // Arrange
        var graph = TestFactory.Graph("A");
        var inputs = TestFactory.OwnInputs();
        var stored = TestFactory.Stored();

        // Act
        var act = () => CachePlanBuilder.Build(graph, inputs, stored);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Build_ShouldMissAndGate_WhenNothingIsStoredForTheProject()
    {
        // Arrange
        var graph = TestFactory.Graph("A");
        var inputs = TestFactory.OwnInputs(("A", ["a=1"]));
        var stored = TestFactory.Stored();

        // Act
        var plan = CachePlanBuilder.Build(graph, inputs, stored);

        // Assert
        var decision = plan[TestFactory.Id("A")];
        Assert.Equal(CacheOutcome.Miss, decision.BuildOutcome);
        Assert.True(decision.ShouldGateCoverage);
        Assert.False(decision.Forced);
    }

    [Fact]
    public void Build_ShouldHitAndNotGate_WhenComputedHashesMatchWhatIsStored()
    {
        // Arrange
        var graph = TestFactory.Graph("A");
        var inputs = TestFactory.OwnInputs(("A", ["a=1"]));
        var ownHash = TargetHasher.ComputeOwnHash(["a=1"]);
        var fullHash = TargetHasher.ComputeFullHash(ownHash, []);
        var stored = TestFactory.Stored(("A", ownHash, fullHash));

        // Act
        var plan = CachePlanBuilder.Build(graph, inputs, stored);

        // Assert
        var decision = plan[TestFactory.Id("A")];
        Assert.Equal(CacheOutcome.Hit, decision.BuildOutcome);
        Assert.False(decision.ShouldGateCoverage);
    }

    [Fact]
    public void Build_ShouldMissAndGate_WhenOwnHashChangedSinceItWasStored()
    {
        // Arrange
        var graph = TestFactory.Graph("A");
        var inputs = TestFactory.OwnInputs(("A", ["a=2"]));
        var staleOwnHash = TargetHasher.ComputeOwnHash(["a=1"]);
        var staleFullHash = TargetHasher.ComputeFullHash(staleOwnHash, []);
        var stored = TestFactory.Stored(("A", staleOwnHash, staleFullHash));

        // Act
        var plan = CachePlanBuilder.Build(graph, inputs, stored);

        // Assert
        var decision = plan[TestFactory.Id("A")];
        Assert.Equal(CacheOutcome.Miss, decision.BuildOutcome);
        Assert.True(decision.ShouldGateCoverage);
    }

    [Fact]
    public void Build_ShouldMissDependentButNotGateIt_WhenOnlyItsDependencyChanged()
    {
        // Arrange
        var graph = TestFactory.Graph("Buhlmann -> Core", "Core");

        var coreOwnHashBefore = TargetHasher.ComputeOwnHash(["core:v1"]);
        var coreFullHashBefore = TargetHasher.ComputeFullHash(coreOwnHashBefore, []);
        var buhlmannOwnHash = TargetHasher.ComputeOwnHash(["buhlmann:v1"]);
        var buhlmannFullHashBefore = TargetHasher.ComputeFullHash(buhlmannOwnHash, [coreFullHashBefore]);

        var stored = TestFactory.Stored(
            ("Core", coreOwnHashBefore, coreFullHashBefore),
            ("Buhlmann", buhlmannOwnHash, buhlmannFullHashBefore));

        var inputs = TestFactory.OwnInputs(
            ("Core", ["core:v2"]),
            ("Buhlmann", ["buhlmann:v1"]));

        // Act
        var plan = CachePlanBuilder.Build(graph, inputs, stored);

        // Assert
        var core = plan[TestFactory.Id("Core")];
        var buhlmann = plan[TestFactory.Id("Buhlmann")];

        Assert.Equal(CacheOutcome.Miss, core.BuildOutcome);
        Assert.True(core.ShouldGateCoverage);

        Assert.Equal(CacheOutcome.Miss, buhlmann.BuildOutcome);
        Assert.False(buhlmann.ShouldGateCoverage);
    }

    [Fact]
    public void Build_ShouldHitDependent_WhenNeitherItNorItsDependencyChanged()
    {
        // Arrange
        var graph = TestFactory.Graph("Buhlmann -> Core", "Core");
        var inputs = TestFactory.UniformOwnInputs(graph);

        var coreOwnHash = TargetHasher.ComputeOwnHash(inputs[TestFactory.Id("Core")]);
        var coreFullHash = TargetHasher.ComputeFullHash(coreOwnHash, []);
        var buhlmannOwnHash = TargetHasher.ComputeOwnHash(inputs[TestFactory.Id("Buhlmann")]);
        var buhlmannFullHash = TargetHasher.ComputeFullHash(buhlmannOwnHash, [coreFullHash]);

        var stored = TestFactory.Stored(
            ("Core", coreOwnHash, coreFullHash),
            ("Buhlmann", buhlmannOwnHash, buhlmannFullHash));

        // Act
        var plan = CachePlanBuilder.Build(graph, inputs, stored);

        // Assert
        Assert.All(plan.Values, decision => Assert.Equal(CacheOutcome.Hit, decision.BuildOutcome));
        Assert.All(plan.Values, decision => Assert.False(decision.ShouldGateCoverage));
    }

    [Fact]
    public void Build_ShouldForceAMiss_WhenProjectIsInTheForcedSet_EvenIfHashesMatch()
    {
        // Arrange
        var graph = TestFactory.Graph("A");
        var inputs = TestFactory.OwnInputs(("A", ["a=1"]));
        var ownHash = TargetHasher.ComputeOwnHash(["a=1"]);
        var fullHash = TargetHasher.ComputeFullHash(ownHash, []);
        var stored = TestFactory.Stored(("A", ownHash, fullHash));
        var forced = new HashSet<ProjectId> { TestFactory.Id("A") };

        // Act
        var plan = CachePlanBuilder.Build(graph, inputs, stored, forced);

        // Assert
        var decision = plan[TestFactory.Id("A")];
        Assert.Equal(CacheOutcome.Miss, decision.BuildOutcome);
        Assert.True(decision.Forced);
        Assert.True(decision.ShouldGateCoverage);
    }

    [Fact]
    public void Build_ShouldMissButNotGate_WhenOnlyAGlobalFullHashInputChanges()
    {
        // Arrange
        var graph = TestFactory.Graph("A");
        var inputs = TestFactory.OwnInputs(("A", ["a=1"]));
        var ownHash = TargetHasher.ComputeOwnHash(["a=1"]);
        var staleFullHash = TargetHasher.ComputeFullHash(ownHash, ["remotebuildtool:v1"]);
        var stored = TestFactory.Stored(("A", ownHash, staleFullHash));

        // Act
        var plan = CachePlanBuilder.Build(graph, inputs, stored, globalFullHashInputs: ["remotebuildtool:v2"]);

        // Assert
        var decision = plan[TestFactory.Id("A")];
        Assert.Equal(CacheOutcome.Miss, decision.BuildOutcome);
        Assert.False(decision.ShouldGateCoverage);
    }

    [Fact]
    public void Build_ShouldHit_WhenGlobalFullHashInputIsUnchanged()
    {
        // Arrange
        var graph = TestFactory.Graph("A");
        var inputs = TestFactory.OwnInputs(("A", ["a=1"]));
        var ownHash = TargetHasher.ComputeOwnHash(["a=1"]);
        var fullHash = TargetHasher.ComputeFullHash(ownHash, ["remotebuildtool:v1"]);
        var stored = TestFactory.Stored(("A", ownHash, fullHash));

        // Act
        var plan = CachePlanBuilder.Build(graph, inputs, stored, globalFullHashInputs: ["remotebuildtool:v1"]);

        // Assert
        Assert.Equal(CacheOutcome.Hit, plan[TestFactory.Id("A")].BuildOutcome);
    }

    [Fact]
    public void Build_ShouldReturnOneDecisionPerProject_WhenGraphHasMultipleProjects()
    {
        // Arrange
        var graph = TestFactory.Graph("A -> C", "B -> C", "C");
        var inputs = TestFactory.UniformOwnInputs(graph);
        var stored = TestFactory.Stored();

        // Act
        var plan = CachePlanBuilder.Build(graph, inputs, stored);

        // Assert
        Assert.Equal(3, plan.Count);
        Assert.Contains(TestFactory.Id("A"), plan.Keys);
        Assert.Contains(TestFactory.Id("B"), plan.Keys);
        Assert.Contains(TestFactory.Id("C"), plan.Keys);
    }

    [Fact]
    public void Build_ShouldReportNoReason_WhenTheProjectIsAHit()
    {
        // Arrange
        var graph = TestFactory.Graph("A");
        var inputs = TestFactory.OwnInputs(("A", ["a=1"]));
        var ownHash = TargetHasher.ComputeOwnHash(["a=1"]);
        var stored = TestFactory.Stored(("A", ownHash, TargetHasher.ComputeFullHash(ownHash, [])));

        // Act
        var decision = CachePlanBuilder.Build(graph, inputs, stored)[TestFactory.Id("A")];

        // Assert
        Assert.Equal(CacheMissReasons.None, decision.MissReasons);
        Assert.Empty(decision.ChangedDependencies);
    }

    [Fact]
    public void Build_ShouldReportOnlyForced_WhenAStoredProjectIsForced()
    {
        // Arrange
        var graph = TestFactory.Graph("A");
        var inputs = TestFactory.OwnInputs(("A", ["a=2"]));
        var stored = TestFactory.Stored(("A", "stale-own", "stale-full"));
        var forced = new HashSet<ProjectId> { TestFactory.Id("A") };

        // Act
        var decision = CachePlanBuilder.Build(graph, inputs, stored, forced)[TestFactory.Id("A")];

        // Assert
        Assert.Equal(CacheMissReasons.Forced, decision.MissReasons);
        Assert.Empty(decision.ChangedDependencies);
    }

    [Fact]
    public void Build_ShouldReportNotCached_WhenNothingIsStoredForTheProject()
    {
        // Arrange
        var graph = TestFactory.Graph("A -> B");
        var inputs = TestFactory.UniformOwnInputs(graph);

        // Act
        var decision = CachePlanBuilder.Build(graph, inputs, TestFactory.Stored())[TestFactory.Id("A")];

        // Assert
        Assert.Equal(CacheMissReasons.NotCached, decision.MissReasons);
        Assert.Empty(decision.ChangedDependencies);
    }

    [Fact]
    public void Build_ShouldReportOwnInputs_WhenOnlyTheProjectsOwnHashChanged()
    {
        // Arrange
        var graph = TestFactory.Graph("A");
        var inputs = TestFactory.OwnInputs(("A", ["a=2"]));
        var stored = TestFactory.Stored(("A", "stale-own", "stale-full"));

        // Act
        var decision = CachePlanBuilder.Build(graph, inputs, stored)[TestFactory.Id("A")];

        // Assert
        Assert.Equal(CacheMissReasons.OwnInputsChanged, decision.MissReasons);
        Assert.Empty(decision.ChangedDependencies);
    }

    [Fact]
    public void Build_ShouldNameTheChangedReference_WhenOnlyADependencyChanged()
    {
        // Arrange
        var graph = TestFactory.Graph("A -> B, C", "B", "C");
        var inputs = TestFactory.UniformOwnInputs(graph);
        var current = CachePlanBuilder.Build(graph, inputs, TestFactory.Stored());
        var a = current[TestFactory.Id("A")].Computed;
        var c = current[TestFactory.Id("C")].Computed;
        var stored = TestFactory.Stored(
            ("A", a.OwnHash, "stale-full"),
            ("B", "stale-own", "stale-full"),
            ("C", c.OwnHash, c.FullHash));

        // Act
        var decision = CachePlanBuilder.Build(graph, inputs, stored)[TestFactory.Id("A")];

        // Assert
        Assert.Equal(CacheMissReasons.DependenciesChanged, decision.MissReasons);
        Assert.Equal([TestFactory.Id("B")], decision.ChangedDependencies);
    }

    [Fact]
    public void Build_ShouldCountAReferenceAsChanged_WhenNothingIsStoredForIt()
    {
        // Arrange
        var graph = TestFactory.Graph("A -> B", "B");
        var inputs = TestFactory.UniformOwnInputs(graph);
        var a = CachePlanBuilder.Build(graph, inputs, TestFactory.Stored())[TestFactory.Id("A")].Computed;
        var stored = TestFactory.Stored(("A", a.OwnHash, "stale-full"));

        // Act
        var decision = CachePlanBuilder.Build(graph, inputs, stored)[TestFactory.Id("A")];

        // Assert
        Assert.Equal(CacheMissReasons.DependenciesChanged, decision.MissReasons);
        Assert.Equal([TestFactory.Id("B")], decision.ChangedDependencies);
    }

    [Fact]
    public void Build_ShouldReportBoth_WhenTheOwnInputsAndAReferenceChanged()
    {
        // Arrange
        var graph = TestFactory.Graph("A -> B", "B");
        var inputs = TestFactory.UniformOwnInputs(graph);
        var stored = TestFactory.Stored(("A", "stale-own", "stale-full"), ("B", "stale-own", "stale-full"));

        // Act
        var decision = CachePlanBuilder.Build(graph, inputs, stored)[TestFactory.Id("A")];

        // Assert
        Assert.Equal(CacheMissReasons.OwnInputsChanged | CacheMissReasons.DependenciesChanged, decision.MissReasons);
        Assert.Equal([TestFactory.Id("B")], decision.ChangedDependencies);
    }

    [Fact]
    public void Build_ShouldReportGlobalInputs_WhenNeitherTheProjectNorItsReferencesChanged()
    {
        // Arrange
        var graph = TestFactory.Graph("A -> B", "B");
        var inputs = TestFactory.UniformOwnInputs(graph);
        var before = CachePlanBuilder.Build(graph, inputs, TestFactory.Stored(), globalFullHashInputs: ["tool:1"])[TestFactory.Id("A")].Computed;
        var after = CachePlanBuilder.Build(graph, inputs, TestFactory.Stored(), globalFullHashInputs: ["tool:2"])[TestFactory.Id("B")].Computed;
        var stored = TestFactory.Stored(("A", before.OwnHash, before.FullHash), ("B", after.OwnHash, after.FullHash));

        // Act
        var decision = CachePlanBuilder.Build(graph, inputs, stored, globalFullHashInputs: ["tool:2"])[TestFactory.Id("A")];

        // Assert
        Assert.Equal(CacheMissReasons.GlobalInputsChanged, decision.MissReasons);
        Assert.Empty(decision.ChangedDependencies);
    }
}
