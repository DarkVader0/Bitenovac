namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class PipelineOutputTests
{
    private static readonly string NewLine = Environment.NewLine;

    [Fact]
    public void Console_ShouldWriteToTheProcessConsole_WhenTheToolRuns()
    {
        // Arrange
        var console = System.Console.Out;

        // Act
        var output = PipelineOutput.Console;

        // Assert
        Assert.Same(console, output.Out);
    }

    [Theory]
    [InlineData(null, null, false, null)]
    [InlineData("false", "", false, null)]
    [InlineData("true", "/runner/summary.md", true, "/runner/summary.md")]
    public void FromEnvironment_ShouldFollowGitHubActions_WhenItsVariablesAreSet(string? actions, string? summary, bool expectedActions, string? expectedSummary)
    {
        // Arrange
        string? Read(string name) => name switch
        {
            "GITHUB_ACTIONS" => actions,
            "GITHUB_STEP_SUMMARY" => summary,
            _ => null,
        };

        // Act
        var output = PipelineOutput.FromEnvironment(TextWriter.Null, TextWriter.Null, Read);

        // Assert
        Assert.Equal((expectedActions, expectedSummary), (output.GitHubActions, output.StepSummaryPath));
    }

    [Fact]
    public void WriteFailure_ShouldWriteAPlainErrorLine_WhenNotUnderGitHubActions()
    {
        // Arrange
        using var error = new StringWriter();
        var output = new PipelineOutput(TextWriter.Null, error);

        // Act
        output.WriteFailure("build failed (exit 1).");

        // Assert
        Assert.Equal($"error: build failed (exit 1).{NewLine}", error.ToString());
    }

    [Fact]
    public void WriteFailure_ShouldNameTheLocation_WhenNotUnderGitHubActionsAndAFileIsGiven()
    {
        // Arrange
        using var error = new StringWriter();
        var output = new PipelineOutput(TextWriter.Null, error);

        // Act
        output.WriteFailure("error CS1002: ; expected", "src/A/A.cs", 3, 7);

        // Assert
        Assert.Equal($"error: src/A/A.cs(3,7): error CS1002: ; expected{NewLine}", error.ToString());
    }

    [Fact]
    public void WriteWarning_ShouldWriteAPlainWarningLine_WhenNotUnderGitHubActions()
    {
        // Arrange
        using var error = new StringWriter();
        var output = new PipelineOutput(TextWriter.Null, error);

        // Act
        output.WriteWarning("coverage is not gated.");

        // Assert
        Assert.Equal($"warning: coverage is not gated.{NewLine}", error.ToString());
        Assert.Null(output.FirstFailure);
    }

    [Fact]
    public void WriteWarning_ShouldNameTheFile_WhenNotUnderGitHubActionsAndAFileIsGiven()
    {
        // Arrange
        using var error = new StringWriter();
        var output = new PipelineOutput(TextWriter.Null, error);

        // Act
        output.WriteWarning("skipped.", "src/A/A.csproj");

        // Assert
        Assert.Equal($"warning: src/A/A.csproj: skipped.{NewLine}", error.ToString());
    }

    [Fact]
    public void WriteWarning_ShouldPointAWarningAnnotationAtTheFile_WhenUnderGitHubActions()
    {
        // Arrange
        using var error = new StringWriter();
        var output = new PipelineOutput(TextWriter.Null, error, GitHubActions: true);

        // Act
        output.WriteWarning("skipped.", "src/A/A.csproj");

        // Assert
        Assert.Equal($"::warning file=src/A/A.csproj::skipped.{NewLine}", error.ToString());
    }

    [Fact]
    public void WriteFailure_ShouldWriteAnEscapedAnnotation_WhenUnderGitHubActions()
    {
        // Arrange
        using var error = new StringWriter();
        var output = new PipelineOutput(TextWriter.Null, error, GitHubActions: true);

        // Act
        output.WriteFailure("100% failed\nsecond line");

        // Assert
        Assert.Equal($"::error::100%25 failed%0Asecond line{NewLine}", error.ToString());
    }

    [Fact]
    public void WriteFailure_ShouldPointTheAnnotationAtTheFile_WhenUnderGitHubActionsAndAFileIsGiven()
    {
        // Arrange
        using var error = new StringWriter();
        var output = new PipelineOutput(TextWriter.Null, error, GitHubActions: true);

        // Act
        output.WriteFailure("error CS1002: ; expected", "src/a,b:c.cs", 3, 7);

        // Assert
        Assert.Equal($"::error file=src/a%2Cb%3Ac.cs,line=3,col=7::error CS1002: ; expected{NewLine}", error.ToString());
    }

    [Fact]
    public void Section_ShouldWriteItsHeadingAndHowLongItTook_WhenNotUnderGitHubActions()
    {
        // Arrange
        using var writer = new StringWriter();
        var output = new PipelineOutput(writer, TextWriter.Null);

        // Act
        using (output.Section("Restoring", collapse: true))
            output.WriteLine("inside");

        // Assert
        var lines = writer.ToString().Split(NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("==> Restoring", lines[0]);
        Assert.Equal("inside", lines[1]);
        Assert.Matches(@"^<== Restoring \(\d+\.\d s\)$", lines[2]);
    }

    [Fact]
    public void Section_ShouldWriteALogGroup_WhenCollapsedUnderGitHubActions()
    {
        // Arrange
        using var writer = new StringWriter();
        var output = new PipelineOutput(writer, TextWriter.Null, GitHubActions: true);

        // Act
        using (output.Section("Restoring", collapse: true))
            output.WriteLine("inside");

        // Assert
        var lines = writer.ToString().Split(NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["::group::Restoring", "inside", "::endgroup::"], lines[..3]);
        Assert.StartsWith("<== Restoring (", lines[3], StringComparison.Ordinal);
    }

    [Fact]
    public void Section_ShouldWriteNoLogGroup_WhenNotCollapsedUnderGitHubActions()
    {
        // Arrange
        using var writer = new StringWriter();
        var output = new PipelineOutput(writer, TextWriter.Null, GitHubActions: true);

        // Act
        using (output.Section("Testing"))
            output.WriteLine("inside");

        // Assert
        Assert.DoesNotContain("::group::", writer.ToString(), StringComparison.Ordinal);
        Assert.StartsWith("==> Testing", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Section_ShouldEndOnce_WhenDisposedTwice()
    {
        // Arrange
        using var writer = new StringWriter();
        var output = new PipelineOutput(writer, TextWriter.Null, GitHubActions: true);
        var section = output.Section("Restoring", collapse: true);

        // Act
        section.Dispose();
        section.Dispose();

        // Assert
        Assert.Single(writer.ToString().Split(NewLine), line => line == "::endgroup::");
        Assert.True(section.Elapsed >= TimeSpan.Zero);
    }

    [Fact]
    public void WriteGroup_ShouldWriteAHeadingAndTheBody_WhenNotUnderGitHubActions()
    {
        // Arrange
        using var writer = new StringWriter();
        var output = new PipelineOutput(writer, TextWriter.Null);

        // Act
        output.WriteGroup("A.Tests: passed", $"body{NewLine}");

        // Assert
        Assert.Equal($"--- A.Tests: passed{NewLine}body{NewLine}", writer.ToString());
    }

    [Fact]
    public void WriteGroup_ShouldFoldTheBodyAway_WhenUnderGitHubActions()
    {
        // Arrange
        using var writer = new StringWriter();
        var output = new PipelineOutput(writer, TextWriter.Null, GitHubActions: true);

        // Act
        output.WriteGroup("A.Tests: passed", $"body{NewLine}");

        // Assert
        Assert.Equal($"::group::A.Tests: passed{NewLine}body{NewLine}::endgroup::{NewLine}", writer.ToString());
    }

    [Fact]
    public void AppendSummary_ShouldAppendLines_WhenThereIsASummaryFile()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var path = directory.Combine("summary.md");
        File.WriteAllText(path, "before\n");
        var output = new PipelineOutput(TextWriter.Null, TextWriter.Null, StepSummaryPath: path);

        // Act
        output.AppendSummary("### Plan");
        output.AppendSummary("done\n");

        // Assert
        Assert.Equal("before\n### Plan\ndone\n", File.ReadAllText(path));
    }

    [Fact]
    public void AppendSummary_ShouldWriteNothing_WhenThereIsNoSummaryFile()
    {
        // Arrange
        using var writer = new StringWriter();
        var output = new PipelineOutput(writer, writer);

        // Act
        output.AppendSummary("### Plan");

        // Assert
        Assert.Empty(writer.ToString());
    }
}
