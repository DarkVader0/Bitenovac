using Bitenovac.RemoteBuildTool.Commands;
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
        Assert.Equal(FingerprintFormat.Version, graph.FingerprintFormat);
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

    [Fact]
    public void CompareGraphs_ShouldListWhatCameWentAndChanged_WhenTheGraphsDiffer()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var before = Write(directory, "old", TestFactory.Identity("1-1"),
            TestFactory.Entry("src/A/A.csproj", ownHashInputs: ["source:src/A/A.cs=1", "source:src/A/Old.cs=2"]),
            TestFactory.Entry("src/B/B.csproj", references: ["src/A/A.csproj"]),
            TestFactory.Entry("src/Gone/Gone.csproj"));
        var after = Write(directory, "new", TestFactory.Identity("2-1"),
            TestFactory.Entry("src/A/A.csproj", ownHashInputs: ["source:src/A/A.cs=9", "source:src/A/New.cs=3"]),
            TestFactory.Entry("src/B/B.csproj", references: ["src/C/C.csproj"]),
            TestFactory.Entry("src/C/C.csproj"));
        var output = new CapturedOutput();

        // Act
        var exitCode = CompareGraphsCommand.Run(before, after, output.Pipeline);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(
            [
                "1-1 (abc123) -> 2-1 (abc123)",
                "Debug:",
                "  + src/C/C.csproj",
                "  - src/Gone/Gone.csproj",
                "  ~ src/A/A.csproj: inputs changed source:src/A/A.cs; added source:src/A/New.cs; removed source:src/A/Old.cs",
                "  ~ src/B/B.csproj: references +src/C/C.csproj, -src/A/A.csproj",
            ],
            output.Out.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void CompareGraphs_ShouldSayNothingChanged_WhenTheGraphsAreAlike()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var before = Write(directory, "old", TestFactory.Identity() with { Sha = null }, TestFactory.Entry("src/A/A.csproj"));
        var after = Write(directory, "new", TestFactory.Identity(), TestFactory.Entry("src/A/A.csproj"));
        var output = new CapturedOutput();

        // Act
        CompareGraphsCommand.Run(before, after, output.Pipeline);

        // Assert
        Assert.Contains("100-1 (unknown commit) -> 100-1 (abc123)", output.Out, StringComparison.Ordinal);
        Assert.Contains("Debug: no change", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void CompareGraphs_ShouldSayTheRulesChangedAndListAWholeConfiguration_WhenOnlyOneGraphHasIt()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var before = Write(directory, "old", TestFactory.Identity(), TestFactory.Entry("src/A/A.csproj"));
        var text = File.ReadAllText(before).Replace($"\"fingerprintFormat\": {FingerprintFormat.Version}", "\"fingerprintFormat\": 1", StringComparison.Ordinal);
        File.WriteAllText(before, text.Replace("\"Debug\"", "\"Release\"", StringComparison.Ordinal));
        var after = Write(directory, "new", TestFactory.Identity(), TestFactory.Entry("src/A/A.csproj"));
        var output = new CapturedOutput();

        // Act
        CompareGraphsCommand.Run(before, after, output.Pipeline);

        // Assert
        Assert.Contains($"fingerprint rules changed (1 -> {FingerprintFormat.Version})", output.Out, StringComparison.Ordinal);
        Assert.Contains($"Debug:{Environment.NewLine}  + src/A/A.csproj", output.Out, StringComparison.Ordinal);
        Assert.Contains($"Release:{Environment.NewLine}  - src/A/A.csproj", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void CompareGraphs_ShouldThrow_WhenAGraphIsMissing()
    {
        // Arrange
        using var directory = TestFactory.Directory();

        // Act
        Action act = () => CompareGraphsCommand.Run(directory.Combine("old.json"), directory.Combine("new.json"), TestFactory.Silence());

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }

    private static string Write(TemporaryDirectory directory, string name, Telemetry.BuildIdentity identity, params PlanEntry[] entries) =>
        BuildGraphFile.Write(directory.Combine(name), identity, new Dictionary<string, List<PlanEntry>> { ["Debug"] = [.. entries] });

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
