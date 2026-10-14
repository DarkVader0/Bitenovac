using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Core.Planning;
using Bitenovac.RemoteBuildTool.Service;
using Bitenovac.RemoteBuildTool.Storage;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class MainStoreWriterTests
{
    private const string Project = "src/A/A.csproj";

    private static readonly DateTimeOffset Start = new(2026, 10, 14, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PromoteAsync_ShouldFillMainAndSaySo_WhenTheRunIsStaged()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var writer = new MainStoreWriter(new ManualTime { Now = Start });
        var run = Run(directory, "run", "from the run", fullHash: "pr", forced: false);

        // Act
        var summary = await writer.PromoteAsync(run, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Promoted 1 entry into main.", summary);
        Assert.Equal("from the run", MainContent(directory));
    }

    [Fact]
    public async Task SwitchAsync_ShouldPromoteAgainOnlyWhatWasPromotedSinceTheRebuildStarted_WhenMainSwitches()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var time = new ManualTime { Now = Start.AddMinutes(-5) };
        var writer = new MainStoreWriter(time);
        await writer.PromoteAsync(Run(directory, "before", "merged before", fullHash: "before", forced: false), TestContext.Current.CancellationToken);
        time.Now = Start.AddMinutes(5);
        await writer.PromoteAsync(Run(directory, "during", "merged during", fullHash: "during", forced: false), TestContext.Current.CancellationToken);
        var rebuild = Run(directory, "rebuild", "rebuilt", fullHash: "rebuilt", forced: true);
        var output = new CapturedOutput();

        // Act
        var exitCode = await writer.SwitchAsync(rebuild, output.Pipeline, Start, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal("merged during", MainContent(directory));
        Assert.Single(output.Out.Split('\n'), line => line.StartsWith("Promoted run during-1 again", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PromoteAsync_ShouldForgetPromotionsOlderThanTheStagingRetention_WhenItPromotesAgain()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var time = new ManualTime { Now = Start.AddDays(-2) };
        var writer = new MainStoreWriter(time);
        await writer.PromoteAsync(Run(directory, "old", "old", fullHash: "old", forced: false), TestContext.Current.CancellationToken);
        time.Now = Start;
        await writer.PromoteAsync(Run(directory, "new", "new", fullHash: "new", forced: false), TestContext.Current.CancellationToken);
        var output = new CapturedOutput();

        // Act
        await writer.SwitchAsync(Run(directory, "rebuild", "rebuilt", fullHash: "rebuilt", forced: true), output.Pipeline, Start.AddDays(-3), TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain("Promoted run old-1", output.Out, StringComparison.Ordinal);
        Assert.Contains("Promoted run new-1", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SwitchAsync_ShouldStillSwitchAndPromoteAgain_WhenNeitherRunNamesItsBuild()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var time = new ManualTime { Now = Start.AddMinutes(5) };
        var writer = new MainStoreWriter(time);
        await writer.PromoteAsync(Run(directory, "during", "merged during", fullHash: "during", forced: false) with { Identity = null }, TestContext.Current.CancellationToken);
        var rebuild = Run(directory, "rebuild", "rebuilt", fullHash: "rebuilt", forced: true) with { Identity = null };
        var output = new CapturedOutput();

        // Act
        var exitCode = await writer.SwitchAsync(rebuild, output.Pipeline, Start, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal("merged during", MainContent(directory));
        Assert.Contains("Promoted run  again, merged while the rebuild ran: Promoted 1 entry into main.", output.Out, StringComparison.Ordinal);
    }

    private static PipelineOptions Run(TemporaryDirectory directory, string name, string content, string fullHash, bool forced)
    {
        var store = directory.Combine($"stores/{name}");
        var source = directory.Combine($"built/{name}");
        TestFactory.WriteFile(Path.Combine(source, "bin", "A.dll"), content);
        new LocalVolumeArtifactStore(store).Put(new ProjectId(Project), "Debug", source, new StoredTargetHash("own", fullHash));
        TestFactory.Plan("Debug", TestFactory.Entry(Project, ownHash: "own", fullHash: fullHash, forced: forced)).Save(Path.Combine(store, "plan.json"));
        return TestFactory.Options(directory.Path, directory.Combine("main"), store) with { Identity = TestFactory.Identity($"{name}-1") };
    }

    private static string MainContent(TemporaryDirectory directory)
    {
        Assert.True(new LocalVolumeArtifactStore(directory.Combine("main")).TryReadText(new ProjectId(Project), "Debug", "bin/A.dll", out var content));
        return content;
    }

    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
