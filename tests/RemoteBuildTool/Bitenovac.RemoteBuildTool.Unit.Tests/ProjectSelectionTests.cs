using Bitenovac.RemoteBuildTool.Toolchains;
using Bitenovac.RemoteBuildTool.Toolchains.DotNet;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class ProjectSelectionTests
{
    private const string SkippedWarning = "skipped: it sets <RemoteBuildToolSkip>true</RemoteBuildToolSkip>, so it is not built, tested or released.";

    private const string UngatedWarning = "coverage is measured but not gated: set <MinimumLineCoverage> and <MinimumBranchCoverage>, 0 to measure without a gate, or <ExcludeFromCoverage>true</ExcludeFromCoverage>.";

    [Fact]
    public void CheckSkipped_ShouldPassWithoutWarning_WhenNothingIsSkipped()
    {
        // Arrange
        var output = new CapturedOutput();
        List<(string, EvaluatedProject)> evaluated = [("Debug", Project("src/A/A.csproj")), ("Release", Project("src/A/A.csproj"))];

        // Act
        var passed = ProjectSelection.CheckSkipped(evaluated, output.Pipeline);

        // Assert
        Assert.True(passed);
        Assert.Empty(output.Error);
    }

    [Fact]
    public void CheckSkipped_ShouldWarnOncePerProject_WhenItIsSkippedInBothConfigurations()
    {
        // Arrange
        var output = new CapturedOutput();
        List<(string, EvaluatedProject)> evaluated =
        [
            ("Debug", Project("src/A/A.csproj")),
            ("Debug", Project("tests/B/B.csproj", skip: true)),
            ("Release", Project("src/A/A.csproj")),
            ("Release", Project("tests/B/B.csproj", skip: true)),
        ];

        // Act
        var passed = ProjectSelection.CheckSkipped(evaluated, output.Pipeline);

        // Assert
        Assert.True(passed);
        Assert.Equal($"warning: tests/B/B.csproj: {SkippedWarning}{Environment.NewLine}", output.Error);
    }

    [Fact]
    public void CheckSkipped_ShouldFail_WhenEveryProjectIsSkipped()
    {
        // Arrange
        var output = new CapturedOutput();
        List<(string, EvaluatedProject)> evaluated = [("Debug", Project("src/A/A.csproj", skip: true))];

        // Act
        var passed = ProjectSelection.CheckSkipped(evaluated, output.Pipeline);

        // Assert
        Assert.False(passed);
        Assert.Contains("error: every project sets <RemoteBuildToolSkip>true</RemoteBuildToolSkip>; there is nothing to plan.", output.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckSkipped_ShouldFailNamingEachReference_WhenAProjectThatStaysReferencesASkippedOne()
    {
        // Arrange
        var output = new CapturedOutput();
        List<(string, EvaluatedProject)> evaluated =
        [
            ("Debug", Project("src/A/A.csproj", skip: true)),
            ("Debug", Project("tests/A/A.Tests.csproj", references: ["src/A/A.csproj"])),
            ("Release", Project("src/A/A.csproj", skip: true)),
            ("Release", Project("tests/A/A.Tests.csproj", references: ["src/A/A.csproj"])),
        ];

        // Act
        var passed = ProjectSelection.CheckSkipped(evaluated, output.Pipeline);

        // Assert
        Assert.False(passed);
        Assert.EndsWith(
            $"error: these projects reference a project that sets <RemoteBuildToolSkip>true</RemoteBuildToolSkip>; skip them too, or stop skipping what they reference:{Environment.NewLine}"
            + $"  tests/A/A.Tests.csproj references src/A/A.csproj{Environment.NewLine}",
            output.Error,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CheckSkipped_ShouldPass_WhenTheReferenceIsOnlySkippedInAnotherConfiguration()
    {
        // Arrange
        var output = new CapturedOutput();
        List<(string, EvaluatedProject)> evaluated =
        [
            ("Debug", Project("src/A/A.csproj")),
            ("Debug", Project("src/B/B.csproj", references: ["src/A/A.csproj"])),
            ("Release", Project("src/A/A.csproj", skip: true)),
            ("Release", Project("src/B/B.csproj", references: ["src/A/A.csproj"], skip: true)),
        ];

        // Act
        var passed = ProjectSelection.CheckSkipped(evaluated, output.Pipeline);

        // Assert
        Assert.True(passed);
    }

    [Fact]
    public void WarnUngatedCoverage_ShouldWarnAboutEachMeasuredProject_WhenAMinimumIsUnset()
    {
        // Arrange
        var output = new CapturedOutput();
        EvaluatedProject[] projects =
        [
            Project("src/B/B.csproj", line: 90, branch: null),
            Project("src/A/A.csproj", line: null, branch: 90),
            Project("src/C/C.csproj", line: 0, branch: 0),
            Project("src/D/D.csproj", line: null, branch: null, excludeFromCoverage: true),
            Project("tests/E/E.csproj", line: null, branch: null, isTestProject: true),
        ];

        // Act
        ProjectSelection.WarnUngatedCoverage(projects, output.Pipeline);

        // Assert
        Assert.Equal(
            $"warning: src/A/A.csproj: {UngatedWarning}{Environment.NewLine}warning: src/B/B.csproj: {UngatedWarning}{Environment.NewLine}",
            output.Error);
    }

    private static EvaluatedProject Project(
        string path,
        IReadOnlyList<string>? references = null,
        bool skip = false,
        double? line = 100,
        double? branch = 100,
        bool excludeFromCoverage = false,
        bool isTestProject = false) =>
        new(
            TestFactory.Id(path),
            path,
            DotNetToolchain.ToolchainName,
            [.. (references ?? []).Select(TestFactory.Id)],
            isTestProject,
            excludeFromCoverage,
            line,
            branch,
            CacheTestResults: true,
            skip,
            Path.GetFileNameWithoutExtension(path),
            new Dictionary<string, string>(),
            []);
}
