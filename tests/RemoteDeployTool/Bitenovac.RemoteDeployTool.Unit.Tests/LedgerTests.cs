namespace Bitenovac.RemoteDeployTool.Unit.Tests;

public sealed class LedgerTests
{
    [Fact]
    public void All_ShouldReturnEveryRowOldestFirst_WhenSeveralDeploymentsWereRecorded()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var ledger = new Ledger(directory.Path);
        ledger.Record([TestFactory.Deployment("github-pages", "Web", "2026.10.08.001", TestFactory.Created.AddHours(2))]);
        ledger.Record([
            TestFactory.Deployment("nuget:feed", "A", "2026.10.07.001", TestFactory.Created),
            TestFactory.Deployment("nuget:feed", "B", "2026.10.07.001", TestFactory.Created.AddHours(1)),
        ]);

        // Act
        var rows = ledger.All();

        // Assert
        Assert.Equal(["A", "B", "Web"], rows.Select(row => row.Artifact));
        Assert.Single(Directory.EnumerateFiles(directory.Combine($"{Ledger.Table}/date=2026-10-07"), "2026.10.08.001.github-pages.*.jsonl"));
    }

    [Fact]
    public void Record_ShouldWriteNothing_WhenThereAreNoRows()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var ledger = new Ledger(directory.Path);

        // Act
        ledger.Record([]);

        // Assert
        Assert.Empty(ledger.All());
        Assert.False(Directory.Exists(directory.Combine(Ledger.Table)));
    }

    [Fact]
    public void LastSucceeded_ShouldReturnTheLatestDeploymentThatWentThrough_WhenLaterOnesFailed()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var ledger = new Ledger(directory.Path);
        ledger.Record([TestFactory.Deployment("staging", "Web", "2026.10.07.001", TestFactory.Created)]);
        ledger.Record([TestFactory.Deployment("staging", "Web", "2026.10.08.001", TestFactory.Created.AddDays(1))]);
        ledger.Record([TestFactory.Deployment("staging", "Web", "2026.10.09.001", TestFactory.Created.AddDays(2)) with { Result = DeploymentInfo.Failed }]);
        ledger.Record([TestFactory.Deployment("production", "Web", "2026.10.09.001", TestFactory.Created.AddDays(2))]);
        ledger.Record([TestFactory.Deployment("staging", "Api", "2026.10.09.001", TestFactory.Created.AddDays(2))]);

        // Act
        var last = ledger.LastSucceeded("staging", "Web");

        // Assert
        Assert.Equal("2026.10.08.001", last?.Drop);
    }

    [Fact]
    public void LastSucceeded_ShouldReturnNothing_WhenTheArtifactNeverWentToTheTarget()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var ledger = new Ledger(directory.Path);
        ledger.Record([TestFactory.Deployment("production", "Web", "2026.10.08.001", TestFactory.Created)]);

        // Act
        var last = ledger.LastSucceeded("staging", "Web");

        // Assert
        Assert.Null(last);
    }
}
