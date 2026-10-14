using Bitenovac.RemoteBuildTool.Commands;
using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Core.Planning;
using Bitenovac.RemoteBuildTool.Planning;
using Bitenovac.RemoteBuildTool.Storage;
using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class SwitchCommandTests
{
    private const string Project = "src/A/A.csproj";

    private static readonly StoredTargetHash Hash = new("own", "full");

    [Fact]
    public void Run_ShouldRecordAMatch_WhenMainHeldTheSameOutputUnderTheSameFingerprint()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = Setup(directory, cached: "same", rebuilt: "same");
        var output = new CapturedOutput();

        // Act
        var exitCode = SwitchCommand.Run(options, output.Pipeline);

        // Assert
        Assert.Equal(0, exitCode);
        var comparison = Assert.Single(Comparisons(options));
        Assert.Equal("match", comparison.Outcome);
        Assert.Equal("100-1", comparison.BuildId);
        Assert.Equal("Debug", comparison.Configuration);
        Assert.Equal(Project, comparison.Project);
        Assert.Equal(Hash.FullHash, comparison.CachedFullHash);
        Assert.Empty(comparison.DifferingFiles);
        Assert.Contains("Main switched: 1 match, 0 mismatch, 0 changed, 0 new.", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldRecordTheDifferingFilesAndSwitchMain_WhenMainHeldOtherOutputUnderTheSameFingerprint()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = Setup(directory, cached: "stale", rebuilt: "fresh");
        var output = new CapturedOutput();

        // Act
        SwitchCommand.Run(options, output.Pipeline);

        // Assert
        var comparison = Assert.Single(Comparisons(options));
        Assert.Equal("mismatch", comparison.Outcome);
        Assert.Equal(["bin/A.dll"], comparison.DifferingFiles);
        Assert.Contains($"  mismatch Debug/{Project}: bin/A.dll", output.Out, StringComparison.Ordinal);
        Assert.Equal("fresh", MainContent(options));
    }

    [Theory]
    [InlineData("older", "changed")]
    [InlineData(null, "new")]
    public void Run_ShouldRecordWhatMainHeldAndSwitchIt_WhenItHeldAnotherFingerprintOrNone(string? cachedFullHash, string expected)
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = Setup(directory, cached: cachedFullHash is null ? null : "old", rebuilt: "fresh", cachedHash: Hash with { FullHash = cachedFullHash ?? "" });

        // Act
        SwitchCommand.Run(options, TestFactory.Silence());

        // Assert
        var comparison = Assert.Single(Comparisons(options));
        Assert.Equal(expected, comparison.Outcome);
        Assert.Equal(cachedFullHash, comparison.CachedFullHash);
        Assert.Empty(comparison.DifferingFiles);
        Assert.Equal("fresh", MainContent(options));
    }

    [Fact]
    public void Run_ShouldDeleteWhatTheRebuildDoesNotHold_WhenMainSwitches()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = Setup(directory, cached: "old", rebuilt: "fresh", cachedHash: Hash with { FullHash = "older" });
        Store(directory, options.MainStoreRoot, "gone", "gone", Hash, "src/Gone/Gone.csproj");
        var main = new LocalVolumeArtifactStore(options.MainStoreRoot);

        // Act
        SwitchCommand.Run(options, TestFactory.Silence());

        // Assert
        Assert.False(main.Contains(new ProjectId("src/Gone/Gone.csproj"), "Debug"));
        Assert.False(Directory.Exists(Path.Combine(options.MainStoreRoot, Project, "Debug", "entries", "older")));
        Assert.True(main.TryGetHash(new ProjectId(Project), "Debug", out var live));
        Assert.Equal(Hash, live);
    }

    [Fact]
    public void Run_ShouldThrowAndLeaveMainAsItWas_WhenThePlanWasNotCacheless()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = Setup(directory, cached: "old", rebuilt: "fresh", forced: false);

        // Act
        Action act = () => SwitchCommand.Run(options, TestFactory.Silence());

        // Assert
        Assert.Throws<InvalidOperationException>(act);
        Assert.Equal("old", MainContent(options));
    }

    [Fact]
    public void Run_ShouldThrowAndLeaveMainAsItWas_WhenAProjectWasNotRebuilt()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = Setup(directory, cached: "old", rebuilt: null);

        // Act
        Action act = () => SwitchCommand.Run(options, TestFactory.Silence());

        // Assert
        Assert.Throws<InvalidOperationException>(act);
        Assert.Equal("old", MainContent(options));
    }

    [Fact]
    public void Run_ShouldThrow_WhenTheRebuildPlannedNothing()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.TelemetryOptions(directory);
        new PlanState(new Dictionary<string, List<PlanEntry>>()).Save(options.PlanFile);

        // Act
        Action act = () => SwitchCommand.Run(options, TestFactory.Silence());

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Differences_ShouldNameEveryBinFileThatDiffersOrOnlyOneHolds_WhenEntriesDiffer()
    {
        // Arrange
        var cached = new Dictionary<string, string>
        {
            ["bin/A.dll"] = "1",
            ["bin/A.pdb"] = "2",
            ["bin/Old.dll"] = "3",
            ["obj/A.cache"] = "4",
        };
        var rebuilt = new Dictionary<string, string>
        {
            ["bin/A.dll"] = "1",
            ["bin/A.pdb"] = "9",
            ["bin/New.dll"] = "5",
            ["obj/A.cache"] = "8",
        };

        // Act
        var differing = SwitchCommand.Differences(cached, rebuilt);

        // Assert
        Assert.Equal(["bin/A.pdb", "bin/New.dll", "bin/Old.dll"], differing);
    }

    private static PipelineOptions Setup(
        TemporaryDirectory directory, string? cached, string? rebuilt, StoredTargetHash? cachedHash = null, bool forced = true)
    {
        var options = TestFactory.TelemetryOptions(directory);
        TestFactory.Plan("Debug", TestFactory.Entry(Project, forced: forced, ownHash: Hash.OwnHash, fullHash: Hash.FullHash)).Save(options.PlanFile);

        if (cached is not null)
            Store(directory, options.MainStoreRoot, "cached", cached, cachedHash ?? Hash);
        if (rebuilt is not null)
            Store(directory, options.PrStoreRoot, "rebuilt", rebuilt, Hash);

        return options;
    }

    private static void Store(TemporaryDirectory directory, string storeRoot, string name, string content, StoredTargetHash hash, string project = Project)
    {
        var source = directory.Combine(name);
        TestFactory.WriteFile(Path.Combine(source, "bin", "A.dll"), content);
        TestFactory.WriteFile(Path.Combine(source, "obj", "A.cache"), Guid.NewGuid().ToString());
        new LocalVolumeArtifactStore(storeRoot).Put(new ProjectId(project), "Debug", source, hash);
    }

    private static string MainContent(PipelineOptions options)
    {
        Assert.True(new LocalVolumeArtifactStore(options.MainStoreRoot).TryReadText(new ProjectId(Project), "Debug", "bin/A.dll", out var content));
        return content;
    }

    private static List<CacheComparisonInfo> Comparisons(PipelineOptions options) =>
        [.. TelemetryWriter.Read(options.TelemetryRoot!, TelemetryWriter.CacheComparisonTable, "100-1.", TelemetryJsonContext.Default.CacheComparisonInfo)];
}
