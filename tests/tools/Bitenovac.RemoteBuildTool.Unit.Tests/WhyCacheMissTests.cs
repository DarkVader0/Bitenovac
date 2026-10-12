using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Core.Hashing;
using Bitenovac.RemoteBuildTool.Core.Planning;
using Bitenovac.RemoteBuildTool.Planning;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class WhyCacheMissTests
{
    [Fact]
    public void Describe_ShouldReturnNull_WhenTheProjectIsAHit()
    {
        // Arrange
        var decision = Decision(CacheMissReasons.None);

        // Act
        var why = WhyCacheMiss.Describe(decision, [], null);

        // Assert
        Assert.Null(why);
    }

    [Theory]
    [InlineData(CacheMissReasons.Forced, "forced: the run ignores the cache")]
    [InlineData(CacheMissReasons.NotCached, "not in main's cache")]
    [InlineData(CacheMissReasons.GlobalInputsChanged, "RemoteBuildTool changed")]
    public void Describe_ShouldNameTheReason_WhenItNeedsNoDetail(CacheMissReasons reasons, string expected)
    {
        // Arrange
        var decision = Decision(reasons);

        // Act
        var why = WhyCacheMiss.Describe(decision, [], null);

        // Assert
        Assert.Equal(expected, why);
    }

    [Fact]
    public void Describe_ShouldSayItCannotTellWhich_WhenMainRecordsNoInputs()
    {
        // Arrange
        var decision = Decision(CacheMissReasons.OwnInputsChanged);

        // Act
        var why = WhyCacheMiss.Describe(decision, ["source:a.cs=2"], null);

        // Assert
        Assert.Equal("own inputs changed (main's entry does not record which)", why);
    }

    [Fact]
    public void Describe_ShouldListChangedAddedAndRemovedInputs_WhenMainRecordsThem()
    {
        // Arrange
        var decision = Decision(CacheMissReasons.OwnInputsChanged);
        string[] stored = ["source:a.cs=1", "source:old.cs=1"];
        string[] current = ["source:a.cs=2", "source:new.cs=1"];

        // Act
        var why = WhyCacheMiss.Describe(decision, current, stored);

        // Assert
        Assert.Equal("own inputs changed source:a.cs; added source:new.cs; removed source:old.cs", why);
    }

    [Fact]
    public void Describe_ShouldSayOnlyThatOwnInputsChanged_WhenTheRecordedInputsAreTheSame()
    {
        // Arrange
        var decision = Decision(CacheMissReasons.OwnInputsChanged);
        string[] inputs = ["source:a.cs=1"];

        // Act
        var why = WhyCacheMiss.Describe(decision, inputs, inputs);

        // Assert
        Assert.Equal("own inputs changed", why);
    }

    [Fact]
    public void Describe_ShouldCountTheRest_WhenMoreThanTenInputsOfOneKindChanged()
    {
        // Arrange
        var decision = Decision(CacheMissReasons.OwnInputsChanged);
        var current = Enumerable.Range(0, 12).Select(index => $"source:{index:00}.cs=1").ToList();

        // Act
        var why = WhyCacheMiss.Describe(decision, current, []);

        // Assert
        var listed = string.Join(", ", Enumerable.Range(0, 10).Select(index => $"source:{index:00}.cs"));
        Assert.Equal($"own inputs added {listed} and 2 more", why);
    }

    [Fact]
    public void Describe_ShouldJoinEveryReason_WhenOwnInputsAndReferencesChanged()
    {
        // Arrange
        var decision = Decision(
            CacheMissReasons.OwnInputsChanged | CacheMissReasons.DependenciesChanged,
            new ProjectId("src/B/B.csproj"),
            new ProjectId("src/C/C.csproj"));

        // Act
        var why = WhyCacheMiss.Describe(decision, ["source:a.cs=2"], ["source:a.cs=1"]);

        // Assert
        Assert.Equal("own inputs changed source:a.cs; references changed: src/B/B.csproj, src/C/C.csproj", why);
    }

    [Fact]
    public void ParseInputs_ShouldReadBackWhatFormatInputsWrote_WhenGivenInputs()
    {
        // Arrange
        string[] inputs = ["source:a.cs=1", "package:A/1.0.0"];

        // Act
        var parsed = WhyCacheMiss.ParseInputs(WhyCacheMiss.FormatInputs(inputs));

        // Assert
        Assert.Equal(inputs, parsed);
    }

    private static TargetDecision Decision(CacheMissReasons reasons, params ProjectId[] changedDependencies) => new()
    {
        Project = new ProjectId("src/A/A.csproj"),
        Computed = new TargetHash("own", "full"),
        Forced = reasons == CacheMissReasons.Forced,
        BuildOutcome = reasons == CacheMissReasons.None ? CacheOutcome.Hit : CacheOutcome.Miss,
        MissReasons = reasons,
        ChangedDependencies = changedDependencies,
        ShouldGateCoverage = false,
    };
}
