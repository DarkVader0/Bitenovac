using Bitenovac.RemoteBuildTool.MsBuild;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class MsBuildErrorsTests
{
    private static readonly string NewLine = Environment.NewLine;

    [Fact]
    public void Report_ShouldReportNothing_WhenTheLogDoesNotExist()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        using var error = new StringWriter();

        // Act
        var count = MsBuildErrors.Report(directory.Combine("missing.log"), directory.Path, new PipelineOutput(TextWriter.Null, error));

        // Assert
        Assert.Equal(0, count);
        Assert.Empty(error.ToString());
    }

    [Fact]
    public void Report_ShouldPointAtTheRepositoryFileOnce_WhenSeveralProjectsReportTheSameError()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var source = Path.Combine(directory.Path, "src", "Lib", "Calculator.cs");
        var log = TestFactory.WriteFile(directory.Combine("errors.log"), string.Join(NewLine,
            $"{source}(1,49): error CS1519: Invalid token 'this' [{Path.Combine(directory.Path, "src", "Lib", "Lib.csproj")}]",
            $"2>{source}(1,49): error CS1519: Invalid token 'this' [{Path.Combine(directory.Path, "tests", "Lib.Tests", "Lib.Tests.csproj")}]",
            ""));
        using var error = new StringWriter();

        // Act
        var count = MsBuildErrors.Report(log, directory.Path, new PipelineOutput(TextWriter.Null, error, GitHubActions: true));

        // Assert
        Assert.Equal(1, count);
        Assert.Equal($"::error file=src/Lib/Calculator.cs,line=1,col=49::error CS1519: Invalid token 'this'{NewLine}", error.ToString());
    }

    [Fact]
    public void Report_ShouldReportTheLineAsItIs_WhenItNamesNoLocation()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var log = TestFactory.WriteFile(directory.Combine("errors.log"), "MSBUILD : error MSB1009: Project file does not exist.");
        using var error = new StringWriter();

        // Act
        var count = MsBuildErrors.Report(log, directory.Path, new PipelineOutput(TextWriter.Null, error));

        // Assert
        Assert.Equal(1, count);
        Assert.Equal($"error: MSBUILD : error MSB1009: Project file does not exist.{NewLine}", error.ToString());
    }

    [Fact]
    public void Report_ShouldNotPointAtAFile_WhenItIsOutsideTheRepository()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        using var elsewhere = TestFactory.Directory();
        var line = $"{Path.Combine(elsewhere.Path, "sdk", "Microsoft.Common.targets")}(12): error MSB3021: Unable to copy.";
        var log = TestFactory.WriteFile(directory.Combine("errors.log"), line);
        using var error = new StringWriter();

        // Act
        MsBuildErrors.Report(log, directory.Path, new PipelineOutput(TextWriter.Null, error));

        // Assert
        Assert.Equal($"error: {line}{NewLine}", error.ToString());
    }

    [Fact]
    public void Report_ShouldKeepTheLineWithoutAColumn_WhenTheErrorNamesOnlyALine()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var line = $"{Path.Combine(directory.Path, "Directory.Build.targets")}(4): error MSB4057: The target does not exist.";
        var log = TestFactory.WriteFile(directory.Combine("errors.log"), line);
        using var error = new StringWriter();

        // Act
        MsBuildErrors.Report(log, directory.Path, new PipelineOutput(TextWriter.Null, error, GitHubActions: true));

        // Assert
        Assert.Equal($"::error file=Directory.Build.targets,line=4::error MSB4057: The target does not exist.{NewLine}", error.ToString());
    }
}
