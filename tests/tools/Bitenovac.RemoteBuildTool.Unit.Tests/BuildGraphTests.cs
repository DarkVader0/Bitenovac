using Bitenovac.RemoteBuildTool.Hashing;
using Bitenovac.RemoteBuildTool.Planning;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class BuildGraphTests
{
    [Fact]
    public void Write_ShouldKeepEveryProjectWithItsReferencesAndInputs_WhenReadBack()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var plan = new Dictionary<string, List<PlanEntry>>
        {
            ["Debug"] =
            [
                TestFactory.Entry("tests/A.Tests/A.Tests.csproj", references: ["src/A/A.csproj"], isTestProject: true, hit: true),
                TestFactory.Entry("src/A/A.csproj", ownHashInputs: ["source:src/A/A.cs=1"], whyCacheMiss: "not in main's cache"),
            ],
        };

        // Act
        var path = BuildGraphFile.Write(directory.Path, TestFactory.Identity(), plan);
        var graph = BuildGraphFile.Read(path);

        // Assert
        Assert.Equal(directory.Combine(BuildGraphFile.FileName), path);
        Assert.Equal("100-1", graph.BuildId);
        Assert.Equal("PR", graph.Queue);
        Assert.Equal("abc123", graph.Sha);
        Assert.Equal(TestFactory.Identity().LogsPath, graph.LogsPath);
        Assert.Equal(ToolVersion.Value, graph.ToolVersion);
        Assert.Equal(
            [
                new BuildGraphNode("src/A/A.csproj", "dotnet", false, [], "own-hash", "full-hash", "miss", "not in main's cache", ["source:src/A/A.cs=1"]),
                new BuildGraphNode("tests/A.Tests/A.Tests.csproj", "dotnet", true, ["src/A/A.csproj"], "own-hash", "full-hash", "hit", null, []),
            ],
            graph.Configurations["Debug"],
            new NodeComparer());
    }

    [Fact]
    public void Read_ShouldThrow_WhenTheFileHoldsNoGraph()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var path = TestFactory.WriteFile(directory.Combine("build-graph.json"), "null");

        // Act
        Action act = () => BuildGraphFile.Read(path);

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }

    private sealed class NodeComparer : IEqualityComparer<BuildGraphNode>
    {
        public bool Equals(BuildGraphNode? x, BuildGraphNode? y) =>
            x is not null && y is not null
            && x with { References = [], OwnHashInputs = [] } == y with { References = [], OwnHashInputs = [] }
            && x.References.SequenceEqual(y.References)
            && x.OwnHashInputs.SequenceEqual(y.OwnHashInputs);

        public int GetHashCode(BuildGraphNode obj) => obj.Project.GetHashCode(StringComparison.Ordinal);
    }
}
