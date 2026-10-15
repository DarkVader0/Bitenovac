using Bitenovac.RemoteBuildTool.Commands;
using Bitenovac.RemoteBuildTool.Planning;

namespace Bitenovac.RemoteBuildTool.Integration.Tests;

public sealed class PlanCommandIntegrationTests
{
    private readonly CapturedOutput _output = new();

    [Fact]
    public void Run_ShouldFail_WhenTheRepositoryHoldsNoProjects()
    {
        // Arrange
        using var fixture = FixtureRepository.CreateEmpty();

        // Act
        var exitCode = PlanCommand.Run(fixture.Options, _output.Pipeline);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.False(File.Exists(fixture.Options.PlanFile));
    }

    [Fact]
    public void Run_ShouldMeasureButNotGateAndCacheTheTests_WhenAProjectSetsNoPolicy()
    {
        // Arrange
        using var fixture = FixtureRepository.Create();
        fixture.Write(FixtureRepository.LibraryProject, """
            <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup>
                    <MinimumLineCoverage></MinimumLineCoverage>
                    <MinimumBranchCoverage></MinimumBranchCoverage>
                    <CacheTestResults></CacheTestResults>
                </PropertyGroup>
            </Project>
            """);

        // Act
        var exitCode = PlanCommand.Run(fixture.Options, _output.Pipeline);

        // Assert
        Assert.Equal(0, exitCode);
        var library = PlanState.Load(fixture.Options.PlanFile).For("Debug").Single(entry => entry.ProjectPath == FixtureRepository.LibraryProject);
        Assert.Equal((0d, 0d, true), (library.MinimumLineCoverage, library.MinimumBranchCoverage, library.CacheTestResults));
        Assert.Contains($"warning: {FixtureRepository.LibraryProject}: coverage is measured but not gated", _output.Errors, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldLeaveTheProjectOutWithAWarning_WhenItSetsRemoteBuildToolSkip()
    {
        // Arrange
        using var fixture = FixtureRepository.Create();
        fixture.Replace(FixtureRepository.TestProject, "<RootNamespace>Lib.Tests</RootNamespace>", "<RootNamespace>Lib.Tests</RootNamespace><RemoteBuildToolSkip>true</RemoteBuildToolSkip>");

        // Act
        var exitCode = PlanCommand.Run(fixture.Options, _output.Pipeline);

        // Assert
        Assert.Equal(0, exitCode);
        var plan = PlanState.Load(fixture.Options.PlanFile);
        foreach (var configuration in PipelineOptions.Configurations)
            Assert.Equal([FixtureRepository.LibraryProject], plan.For(configuration).Select(entry => entry.ProjectPath));
        Assert.Contains($"warning: {FixtureRepository.TestProject}: skipped:", _output.Errors, StringComparison.Ordinal);
        Assert.False(File.Exists(fixture.Combine("tests/Lib.Tests/obj/project.assets.json")));
    }

    [Fact]
    public void Run_ShouldFailBeforeRestoring_WhenAProjectThatStaysReferencesASkippedOne()
    {
        // Arrange
        using var fixture = FixtureRepository.Create();
        fixture.Write(FixtureRepository.LibraryProject, """
            <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup>
                    <RemoteBuildToolSkip>true</RemoteBuildToolSkip>
                </PropertyGroup>
            </Project>
            """);

        // Act
        var exitCode = PlanCommand.Run(fixture.Options, _output.Pipeline);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains($"  {FixtureRepository.TestProject} references {FixtureRepository.LibraryProject}", _output.Errors, StringComparison.Ordinal);
        Assert.False(File.Exists(fixture.Combine("src/Lib/obj/project.assets.json")));
        Assert.False(File.Exists(fixture.Options.PlanFile));
    }

    [Fact]
    public void Run_ShouldNameTheProject_WhenItsMinimumIsNotAPercentage()
    {
        // Arrange
        using var fixture = FixtureRepository.Create();
        fixture.Write(FixtureRepository.LibraryProject, """
            <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup>
                    <MinimumLineCoverage>95,5</MinimumLineCoverage>
                </PropertyGroup>
            </Project>
            """);

        // Act
        Action act = () => PlanCommand.Run(fixture.Options, _output.Pipeline);

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Equal("src/Lib/Lib.csproj: MinimumLineCoverage must be a percentage from 0 to 100, such as 95.5; it is '95,5'.", exception.Message);
    }

    [Fact]
    public void Run_ShouldFailBeforeRestoring_WhenAProjectDeclaresTargetFrameworks()
    {
        // Arrange
        using var fixture = FixtureRepository.Create();
        fixture.Write(FixtureRepository.LibraryProject, $"""
            <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup>
                    <TargetFrameworks>{FixtureRepository.TargetFramework};netstandard2.0</TargetFrameworks>
                </PropertyGroup>
            </Project>
            """);

        // Act
        var exitCode = PlanCommand.Run(fixture.Options, _output.Pipeline);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.False(File.Exists(fixture.Combine("src/Lib/obj/project.assets.json")));
        Assert.False(File.Exists(fixture.Options.PlanFile));
    }

    [Fact]
    public void Run_ShouldPlanBothConfigurations_WhenTheRepositoryIsValid()
    {
        // Arrange
        using var fixture = FixtureRepository.Create();

        // Act
        var exitCode = PlanCommand.Run(fixture.Options, _output.Pipeline);

        // Assert
        Assert.Equal(0, exitCode);
        var plan = Planning.PlanState.Load(fixture.Options.PlanFile);
        foreach (var configuration in PipelineOptions.Configurations)
        {
            var entries = plan.For(configuration);
            Assert.Equal(2, entries.Count);
            Assert.Contains(entries, entry => entry.ProjectPath == FixtureRepository.LibraryProject && !entry.IsTestProject);
            Assert.Contains(entries, entry => entry.ProjectPath == FixtureRepository.TestProject && entry.IsTestProject);
        }

        var debug = plan.For("Debug").Single(entry => entry.ProjectPath == FixtureRepository.LibraryProject);
        var release = plan.For("Release").Single(entry => entry.ProjectPath == FixtureRepository.LibraryProject);
        Assert.NotEqual(debug.FullHash, release.FullHash);
    }

    [Fact]
    public void Graph_ShouldPrintEveryProjectAndItsDependencies_ForBothConfigurations()
    {
        // Arrange
        using var fixture = FixtureRepository.Create();

        // Act
        var exitCode = GraphCommand.Run(fixture.Options, _output.Pipeline);
        var reported = _output.ToString();

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("=== Debug ===", reported, StringComparison.Ordinal);
        Assert.Contains("=== Release ===", reported, StringComparison.Ordinal);
        Assert.Contains($"    -> {FixtureRepository.LibraryProject}", reported, StringComparison.Ordinal);
    }
}
