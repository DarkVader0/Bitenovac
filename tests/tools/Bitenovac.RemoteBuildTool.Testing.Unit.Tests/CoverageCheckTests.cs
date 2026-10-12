using Bitenovac.RemoteBuildTool.Core.Coverage;
using Bitenovac.RemoteBuildTool.Core.Graph;

namespace Bitenovac.RemoteBuildTool.Testing.Unit.Tests;

public sealed class CoverageCheckTests
{
    [Fact]
    public void Run_ShouldPass_WhenNoProjectIsGated()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var location = new CoverageReportLocation(directory.Path, directory.Combine("coverage"), directory.Combine("report"), IncludeHtml: false);
        using var output = new StringWriter();

        // Act
        var exitCode = CoverageCheck.Run(
            new Dictionary<ProjectId, CoveragePolicy>(),
            new Dictionary<ProjectId, string>(),
            location,
            output,
            TextWriter.Null);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("nothing to check", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldFail_WhenAGatedRunLeftNoReports()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        System.IO.Directory.CreateDirectory(directory.Combine("coverage"));
        var location = new CoverageReportLocation(directory.Path, directory.Combine("coverage"), directory.Combine("report"), IncludeHtml: false);
        var project = new ProjectId("src/A/A.csproj");
        using var error = new StringWriter();

        // Act
        var exitCode = CoverageCheck.Run(
            new Dictionary<ProjectId, CoveragePolicy> { [project] = new(100, 100) },
            new Dictionary<ProjectId, string> { [project] = "A" },
            location,
            TextWriter.Null,
            error);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("no coverage reports under", error.ToString(), StringComparison.Ordinal);
    }
}
