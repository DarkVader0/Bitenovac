using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class TelemetryWriterTests
{
    private static readonly DateTimeOffset Day = new(2026, 10, 14, 23, 30, 0, TimeSpan.FromHours(-2));

    [Fact]
    public void Write_ShouldPutOneJsonLinePerRecordUnderTheTablesUtcDay_WhenThereAreRecords()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestInfo[] tests =
        [
            new() { Project = "tests/A/A.csproj", TestName = "A.First", Outcome = "Passed" },
            new() { Project = "tests/A/A.csproj", TestName = "A.Second", Outcome = "Failed" },
        ];

        // Act
        var path = TelemetryWriter.Write(directory.Path, TelemetryWriter.TestTable, "100-1.test-Debug.host-1", Day, tests, TelemetryJsonContext.Default.TestInfo);

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
        var path = TelemetryWriter.Write(directory.Path, TelemetryWriter.TestTable, "100-1", Day, [], TelemetryJsonContext.Default.TestInfo);

        // Assert
        Assert.Null(path);
        Assert.False(Directory.Exists(directory.Combine("test")));
    }

    [Fact]
    public void Write_ShouldKeepTheFileInsideTheTable_WhenTheNameCarriesPathCharacters()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestInfo[] tests = [new() { Project = "p", TestName = "t", Outcome = "Passed" }];

        // Act
        var path = TelemetryWriter.Write(directory.Path, TelemetryWriter.TestTable, "../x/y*z", Day, tests, TelemetryJsonContext.Default.TestInfo);

        // Assert
        Assert.StartsWith(".._x_y_z.", Path.GetFileName(path), StringComparison.Ordinal);
        Assert.Equal(directory.Combine("test/date=2026-10-15"), Path.GetDirectoryName(path));
    }

    [Fact]
    public void Read_ShouldReturnTheRecordsOfTheFilesWithThePrefix_WhenSeveralBuildsWrote()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TelemetryWriter.Write(directory.Path, TelemetryWriter.TestTable, "100-1.test-Debug.host-1", Day,
            [new TestInfo { Project = "p", TestName = "Mine", Outcome = "Passed" }], TelemetryJsonContext.Default.TestInfo);
        TelemetryWriter.Write(directory.Path, TelemetryWriter.TestTable, "100-1.test-Release.host-2", Day.AddDays(1),
            [new TestInfo { Project = "p", TestName = "AlsoMine", Outcome = "Passed" }], TelemetryJsonContext.Default.TestInfo);
        TelemetryWriter.Write(directory.Path, TelemetryWriter.TestTable, "1000-1.test-Debug.host-1", Day,
            [new TestInfo { Project = "p", TestName = "NotMine", Outcome = "Passed" }], TelemetryJsonContext.Default.TestInfo);

        // Act
        var names = TelemetryWriter.Read(directory.Path, TelemetryWriter.TestTable, "100-1.", TelemetryJsonContext.Default.TestInfo)
            .Select(test => test.TestName)
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
        var records = TelemetryWriter.Read(directory.Path, TelemetryWriter.StageTable, "100-1.", TelemetryJsonContext.Default.StageInfo);

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
        var records = TelemetryWriter.Read(directory.Path, TelemetryWriter.TestTable, "100-1.", TelemetryJsonContext.Default.TestInfo);

        // Assert
        Assert.Single(records);
    }
}
