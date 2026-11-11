using System.Text.Json.Serialization;

namespace Bitenovac.RemoteTools.Common.Unit.Tests;

public sealed class TelemetryWriterTests
{
    private const string TestTable = "test";

    private static readonly DateTimeOffset Day = new(2026, 10, 14, 23, 30, 0, TimeSpan.FromHours(-2));

    [Fact]
    public void Write_ShouldPutOneJsonLinePerRecordUnderTheTablesUtcDay_WhenThereAreRecords()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TelemetryRecord[] tests =
        [
            new("A.First"),
            new("A.Second"),
        ];

        // Act
        var path = TelemetryWriter.Write(directory.Path, TestTable, "100-1.test-Debug.host-1", Day, tests, TelemetryRecordJsonContext.Default.TelemetryRecord);

        // Assert
        Assert.NotNull(path);
        Assert.Equal(directory.Combine("test/date=2026-10-15"), Path.GetDirectoryName(path));
        Assert.StartsWith("100-1.test-Debug.host-1.", Path.GetFileName(path), StringComparison.Ordinal);
        Assert.EndsWith(".jsonl", path, StringComparison.Ordinal);
        Assert.Equal(2, File.ReadAllLines(path).Length);
        Assert.Empty(Directory.EnumerateFiles(directory.Path, "*.partial", SearchOption.AllDirectories));
    }

    [Fact]
    public void Write_ShouldWriteNothing_WhenThereAreNoRecords()
    {
        // Arrange
        using var directory = TestFactory.Directory();

        // Act
        var path = TelemetryWriter.Write(directory.Path, TestTable, "100-1", Day, [], TelemetryRecordJsonContext.Default.TelemetryRecord);

        // Assert
        Assert.Null(path);
        Assert.False(Directory.Exists(directory.Combine("test")));
    }

    [Fact]
    public void Write_ShouldKeepTheFileInsideTheTable_WhenTheNameCarriesPathCharacters()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TelemetryRecord[] tests = [new("t")];

        // Act
        var path = TelemetryWriter.Write(directory.Path, TestTable, "../x/y*z", Day, tests, TelemetryRecordJsonContext.Default.TelemetryRecord);

        // Assert
        Assert.StartsWith(".._x_y_z.", Path.GetFileName(path), StringComparison.Ordinal);
        Assert.Equal(directory.Combine("test/date=2026-10-15"), Path.GetDirectoryName(path));
    }

    [Fact]
    public void Read_ShouldReturnTheRecordsOfTheFilesWithThePrefix_WhenSeveralBuildsWrote()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TelemetryWriter.Write(directory.Path, TestTable, "100-1.test-Debug.host-1", Day,
            [new TelemetryRecord("Mine")], TelemetryRecordJsonContext.Default.TelemetryRecord);
        TelemetryWriter.Write(directory.Path, TestTable, "100-1.test-Release.host-2", Day.AddDays(1),
            [new TelemetryRecord("AlsoMine")], TelemetryRecordJsonContext.Default.TelemetryRecord);
        TelemetryWriter.Write(directory.Path, TestTable, "1000-1.test-Debug.host-1", Day,
            [new TelemetryRecord("NotMine")], TelemetryRecordJsonContext.Default.TelemetryRecord);

        // Act
        var names = TelemetryWriter.Read(directory.Path, TestTable, "100-1.", TelemetryRecordJsonContext.Default.TelemetryRecord)
            .Select(test => test.Name)
            .Order(StringComparer.Ordinal);

        // Assert
        Assert.Equal(["AlsoMine", "Mine"], names);
    }

    [Fact]
    public void Read_ShouldReturnNothing_WhenTheTableDoesNotExist()
    {
        // Arrange
        using var directory = TestFactory.Directory();

        // Act
        var records = TelemetryWriter.Read(directory.Path, "stage", "100-1.", TelemetryRecordJsonContext.Default.TelemetryRecord);

        // Assert
        Assert.Empty(records);
    }

    [Fact]
    public void Read_ShouldSkipBlankLines_WhenAFileHasThem()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestFactory.WriteFile(directory.Combine("test/date=2026-10-15/100-1.x.jsonl"),
            "{\"Project\":\"p\",\"TestName\":\"t\",\"Outcome\":\"Passed\"}\n\n");

        // Act
        var records = TelemetryWriter.Read(directory.Path, TestTable, "100-1.", TelemetryRecordJsonContext.Default.TelemetryRecord);

        // Assert
        Assert.Single(records);
    }
}

internal sealed record TelemetryRecord(string Name);

[JsonSerializable(typeof(TelemetryRecord))]
internal sealed partial class TelemetryRecordJsonContext : JsonSerializerContext;
