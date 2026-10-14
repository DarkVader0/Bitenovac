using Bitenovac.RemoteBuildTool.Commands;
using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Core.Planning;
using Bitenovac.RemoteBuildTool.Storage;
using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class AuditCommandTests
{
    private const string Project = "src/A/A.csproj";

    private static readonly StoredTargetHash Hash = new("own", "full");

    [Fact]
    public void Run_ShouldPass_WhenTheRebuildMatchesMainUnderTheSameFingerprint()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = Setup(directory, cached: "same", rebuilt: "same");
        var output = new CapturedOutput();

        // Act
        var exitCode = AuditCommand.Run(options, "Debug", output.Pipeline);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Debug: 1 match, 0 mismatch, 0 not comparable.", output.Out, StringComparison.Ordinal);
        var target = Assert.Single(TelemetryWriter.Read(options.TelemetryRoot!, TelemetryWriter.TargetTable, "100-1.", TelemetryJsonContext.Default.TargetInfo));
        Assert.Equal("audit", target.Kind);
        Assert.Equal("match", target.CacheOutcome);
        Assert.Equal("audit-Debug", target.Stage);
    }

    [Fact]
    public void Run_ShouldFailNamingTheFiles_WhenMainHoldsOtherOutputUnderTheSameFingerprint()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = Setup(directory, cached: "stale", rebuilt: "fresh");
        var output = new CapturedOutput();

        // Act
        var exitCode = AuditCommand.Run(options, "Debug", output.Pipeline);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains($"error: {Project}: main holds different output under the same fingerprint (bin/A.dll)", output.Error, StringComparison.Ordinal);
        Assert.Equal("mismatch", Assert.Single(TelemetryWriter.Read(options.TelemetryRoot!, TelemetryWriter.TargetTable, "100-1.", TelemetryJsonContext.Default.TargetInfo)).CacheOutcome);
    }

    [Fact]
    public void Run_ShouldSkipTheProject_WhenMainHoldsItUnderAnotherFingerprint()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = Setup(directory, cached: "old", rebuilt: "new", cachedHash: Hash with { FullHash = "older" });
        var output = new CapturedOutput();

        // Act
        var exitCode = AuditCommand.Run(options, "Debug", output.Pipeline);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains($"  skip {Project}: main holds it under another fingerprint", output.Out, StringComparison.Ordinal);
        Assert.Contains("0 match, 0 mismatch, 1 not comparable", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldSkipTheProject_WhenMainHoldsNoEntryForIt()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = Setup(directory, cached: null, rebuilt: "new");
        var output = new CapturedOutput();

        // Act
        var exitCode = AuditCommand.Run(options, "Debug", output.Pipeline);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains($"  skip {Project}: main holds no entry", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldThrow_WhenThePlanWasNotCacheless()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.TelemetryOptions(directory);
        TestFactory.Plan("Debug", TestFactory.Entry(Project, forced: false)).Save(options.PlanFile);

        // Act
        Action act = () => AuditCommand.Run(options, "Debug", TestFactory.Silence());

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Run_ShouldThrow_WhenAComparableProjectWasNotRebuilt()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = Setup(directory, cached: "same", rebuilt: null);

        // Act
        Action act = () => AuditCommand.Run(options, "Debug", TestFactory.Silence());

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
        var differing = AuditCommand.Differences(cached, rebuilt);

        // Assert
        Assert.Equal(["bin/A.pdb", "bin/New.dll", "bin/Old.dll"], differing);
    }

    private static PipelineOptions Setup(TemporaryDirectory directory, string? cached, string? rebuilt, StoredTargetHash? cachedHash = null)
    {
        var options = TestFactory.TelemetryOptions(directory);
        TestFactory.Plan("Debug", TestFactory.Entry(Project, forced: true, ownHash: Hash.OwnHash, fullHash: Hash.FullHash)).Save(options.PlanFile);

        if (cached is not null)
            Store(directory, options.MainStoreRoot, "cached", cached, cachedHash ?? Hash);
        if (rebuilt is not null)
            Store(directory, options.PrStoreRoot, "rebuilt", rebuilt, Hash);

        return options;
    }

    private static void Store(TemporaryDirectory directory, string storeRoot, string name, string content, StoredTargetHash hash)
    {
        var source = directory.Combine(name);
        TestFactory.WriteFile(Path.Combine(source, "bin", "A.dll"), content);
        TestFactory.WriteFile(Path.Combine(source, "obj", "A.cache"), Guid.NewGuid().ToString());
        new LocalVolumeArtifactStore(storeRoot).Put(new ProjectId(Project), "Debug", source, hash);
    }
}
