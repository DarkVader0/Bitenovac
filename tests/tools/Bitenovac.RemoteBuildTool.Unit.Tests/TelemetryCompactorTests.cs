using Bitenovac.RemoteBuildTool.Telemetry;
using DuckDB.NET.Data;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class TelemetryCompactorTests
{
    private static readonly DateOnly Today = new(2026, 10, 14);

    private static readonly DateTimeOffset Yesterday = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Compact_ShouldTurnEarlierDaysIntoParquetAndKeepTodayRaw_WhenBothHaveRecords()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var root = directory.Combine("telemetry");
        WriteStage(root, "1-1", Yesterday);
        WriteStage(root, "2-1", Today.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc));

        // Act
        var compacted = TelemetryCompactor.Compact(root, Today);

        // Assert
        Assert.Equal(["stage 2026-10-13"], compacted);
        Assert.False(Directory.Exists(Path.Combine(root, "stage", "date=2026-10-13")));
        Assert.Single(Directory.EnumerateFiles(Path.Combine(root, "parquet", "stage", "date=2026-10-13"), "*.parquet"));
        Assert.Single(Directory.EnumerateFiles(Path.Combine(root, "stage", "date=2026-10-14"), "*.jsonl"));
        Assert.Equal(["1-1", "2-1"], Query(root, "SELECT BuildId FROM stage ORDER BY BuildId"));
    }

    [Fact]
    public void Compact_ShouldGiveEveryTableAnEmptyView_WhenNothingWasRecorded()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var root = directory.Combine("telemetry");

        // Act
        var compacted = TelemetryCompactor.Compact(root, Today);

        // Assert
        Assert.Empty(compacted);
        Assert.Equal(["0"], Query(root, "SELECT count(*) FROM build"));
        Assert.Equal(["0"], Query(root, "SELECT count(*) FROM flaky_tests"));
        Assert.Equal(["0"], Query(root, "SELECT count(*) FROM build_stages('1-1')"));
    }

    [Fact]
    public void Compact_ShouldKeepWhatItCompactedAndSkipAnEmptyDay_WhenItRunsAgain()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var root = directory.Combine("telemetry");
        WriteStage(root, "1-1", Yesterday);
        TelemetryCompactor.Compact(root, Today);
        Directory.CreateDirectory(Path.Combine(root, "stage", "date=2026-10-14"));

        // Act
        var compacted = TelemetryCompactor.Compact(root, Today.AddDays(1));

        // Assert
        Assert.Empty(compacted);
        Assert.Equal(["1-1"], Query(root, "SELECT BuildId FROM stage"));
    }

    [Fact]
    public void Compact_ShouldFinishWhatAStoppedRunMovedAside_WhenItRunsAgain()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var root = directory.Combine("telemetry");
        WriteStage(root, "1-1", Yesterday);
        var raw = Path.Combine(root, "stage", "date=2026-10-13");
        var compacting = Path.Combine(root, "stage", ".compacting-2026-10-13");
        Directory.Move(raw, compacting);

        // Act
        var compacted = TelemetryCompactor.Compact(root, Today);

        // Assert
        Assert.Equal(["stage 2026-10-13"], compacted);
        Assert.False(Directory.Exists(compacting));
        Assert.Equal(["1-1"], Query(root, "SELECT BuildId FROM stage"));
    }

    [Fact]
    public void Compact_ShouldReadAFieldItDoesNotKnowAsNothing_WhenARecordHasOne()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var root = directory.Combine("telemetry");
        TestFactory.WriteFile(Path.Combine(root, "test", "date=2026-10-13", "1-1.test.m.x.jsonl"),
            """{"BuildId":"1-1","Project":"p","TestName":"t","Outcome":"Passed","Later":7}""" + "\n");

        // Act
        TelemetryCompactor.Compact(root, Today);

        // Assert
        Assert.Equal(["t Passed"], Query(root, "SELECT TestName || ' ' || Outcome FROM test"));
    }

    private static void WriteStage(string root, string buildId, DateTimeOffset start) =>
        TelemetryWriter.Write(root, TelemetryWriter.StageTable, $"{buildId}.plan.host-1", start,
            [
                new StageInfo
                {
                    BuildId = buildId,
                    Queue = "PR",
                    Pool = "docker",
                    Machine = "host-1",
                    Stage = "plan",
                    StartTime = start,
                    EndTime = start.AddSeconds(5),
                    DurationMS = 5000,
                    Succeeded = true,
                },
            ],
            TelemetryJsonContext.Default.StageInfo);

    private static List<string> Query(string root, string sql)
    {
        using var connection = new DuckDBConnection($"Data Source={Path.Combine(root, TelemetryCompactor.DatabaseFileName)};ACCESS_MODE=READ_ONLY");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();

        var rows = new List<string>();
        while (reader.Read())
            rows.Add(Convert.ToString(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture)!);

        return rows;
    }
}
