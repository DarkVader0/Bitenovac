namespace Bitenovac.RemoteBuildTool.Testing.Unit.Tests;

public sealed class TestRunnerTests
{
    private static readonly string[] Coverage =
    [
        "--coverlet",
        "--coverlet-output-format", "cobertura",
        "--coverlet-file-prefix", "Lib.Tests",
        "--results-directory", "results",
    ];

    [Fact]
    public void Arguments_ShouldAskOnlyForCoverage_WhenTheProjectWritesNoReport()
    {
        // Arrange

        // Act
        var arguments = TestRunner.Arguments("Lib.Tests", "results", TestReport.None);

        // Assert
        Assert.Equal(Coverage, arguments);
    }

    [Fact]
    public void Arguments_ShouldAskForTheTestPlatformsTrx_WhenTheProjectReferencesTheTrxReport()
    {
        // Arrange

        // Act
        var arguments = TestRunner.Arguments("Lib.Tests", "results", TestReport.Trx);

        // Assert
        Assert.Equal([.. Coverage, "--report-trx", "--report-trx-filename", "Lib.Tests.trx"], arguments);
    }

    [Fact]
    public void Arguments_ShouldAskForXunitsTrx_WhenTheProjectRunsXunit()
    {
        // Arrange

        // Act
        var arguments = TestRunner.Arguments("Lib.Tests", "results", TestReport.XunitTrx);

        // Assert
        Assert.Equal([.. Coverage, "--report-xunit-trx", "--report-xunit-trx-filename", "Lib.Tests.trx"], arguments);
    }

    [Fact]
    public void ReportPath_ShouldBeTheReport_WhenOneWasAskedForAndWritten()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var report = TestFactory.WriteFile(Path.Combine(directory.Path, "Lib.Tests.trx"), "<TestRun/>");

        // Act
        var path = TestRunner.ReportPath("Lib.Tests", directory.Path, TestReport.XunitTrx);

        // Assert
        Assert.Equal(report, path);
    }

    [Fact]
    public void ReportPath_ShouldBeNull_WhenTheReportWasAskedForButNotWritten()
    {
        // Arrange
        using var directory = TestFactory.Directory();

        // Act
        var path = TestRunner.ReportPath("Lib.Tests", directory.Path, TestReport.Trx);

        // Assert
        Assert.Null(path);
    }

    [Fact]
    public void ReportPath_ShouldBeNull_WhenNoReportWasAskedFor()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestFactory.WriteFile(Path.Combine(directory.Path, "Lib.Tests.trx"), "<TestRun/>");

        // Act
        var path = TestRunner.ReportPath("Lib.Tests", directory.Path, TestReport.None);

        // Assert
        Assert.Null(path);
    }
}
