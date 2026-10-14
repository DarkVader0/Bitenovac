using Bitenovac.RemoteBuildTool.Commands;
using Bitenovac.RemoteBuildTool.Planning;

namespace Bitenovac.RemoteBuildTool.Integration.Tests;

public sealed class ImportFingerprintIntegrationTests
{
    private readonly CapturedOutput _output = new();

    [Fact]
    public void Plan_ShouldHitEveryProject_WhenNothingChanged()
    {
        // Arrange
        using var fixture = FixtureRepository.CreateWarm();

        // Act
        var (library, tests) = Plan(fixture);

        // Assert
        Assert.True(library.Hit, library.WhyCacheMiss);
        Assert.True(tests.Hit, tests.WhyCacheMiss);
    }

    [Fact]
    public void Plan_ShouldHitEveryProject_WhenOnlyACommentInARepositoryFileChanges()
    {
        // Arrange
        using var fixture = FixtureRepository.CreateWarm();
        fixture.Replace("Directory.Build.props", "</Project>", "    <!-- A note that builds nothing. -->\n</Project>");

        // Act
        var (library, tests) = Plan(fixture);

        // Assert
        Assert.True(library.Hit, library.WhyCacheMiss);
        Assert.True(tests.Hit, tests.WhyCacheMiss);
    }

    [Fact]
    public void Plan_ShouldMissOnlyTheTests_WhenAPropertyOnlyTestProjectsSeeChanges()
    {
        // Arrange
        using var fixture = FixtureRepository.CreateWarm();
        fixture.Replace("Directory.Build.props", "<FixtureTestSetting>1<", "<FixtureTestSetting>2<");

        // Act
        var (library, tests) = Plan(fixture);

        // Assert
        Assert.True(library.Hit, library.WhyCacheMiss);
        Assert.False(tests.Hit);
        Assert.Contains("repo-property:FixtureTestSetting", tests.WhyCacheMiss, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_ShouldMissEveryProject_WhenAPropertyEveryProjectSeesChanges()
    {
        // Arrange
        using var fixture = FixtureRepository.CreateWarm();
        fixture.Replace("Directory.Build.props", "<FixtureSharedSetting>1<", "<FixtureSharedSetting>2<");

        // Act
        var (library, tests) = Plan(fixture);

        // Assert
        Assert.False(library.Hit);
        Assert.False(tests.Hit);
        Assert.Contains("repo-property:FixtureSharedSetting", library.WhyCacheMiss, StringComparison.Ordinal);
        Assert.Contains("repo-property:FixtureSharedSetting", tests.WhyCacheMiss, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_ShouldMissEveryProject_WhenARepositoryItemDefinitionChanges()
    {
        // Arrange
        using var fixture = FixtureRepository.CreateWarm();
        fixture.Replace("Directory.Build.props", "<FixtureMetadata>1<", "<FixtureMetadata>2<");

        // Act
        var (library, tests) = Plan(fixture);

        // Assert
        Assert.False(library.Hit);
        Assert.False(tests.Hit);
        Assert.Contains("repo-item-definition:Compile:FixtureMetadata", library.WhyCacheMiss, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_ShouldMissEveryImporter_WhenARepositoryTargetChanges()
    {
        // Arrange
        using var fixture = FixtureRepository.CreateWarm();
        fixture.Replace("Directory.Build.targets", """<Target Name="FixtureProbe"/>""",
            """<Target Name="FixtureProbe"><Message Text="changed"/></Target>""");

        // Act
        var (library, tests) = Plan(fixture);

        // Assert
        Assert.False(library.Hit);
        Assert.False(tests.Hit);
        Assert.Contains("repo-logic:Directory.Build.targets", library.WhyCacheMiss, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_ShouldMissTheProjectAndEverythingAboveIt_WhenAPropertyInItsProjectFileChanges()
    {
        // Arrange
        using var fixture = FixtureRepository.CreateWarm();
        fixture.Replace(FixtureRepository.LibraryProject, "<Description>1<", "<Description>2<");

        // Act
        var (library, tests) = Plan(fixture);

        // Assert
        Assert.False(library.Hit);
        Assert.False(tests.Hit);
        Assert.Contains("repo-property:Description", library.WhyCacheMiss, StringComparison.Ordinal);
        Assert.Equal($"references changed: {FixtureRepository.LibraryProject}", tests.WhyCacheMiss);
    }

    private (PlanEntry Library, PlanEntry Tests) Plan(FixtureRepository fixture)
    {
        fixture.With(prStoreRoot: fixture.Combine("artifacts/ci-pr-2"));
        Assert.Equal(0, PlanCommand.Run(fixture.Options, _output.Pipeline));

        var entries = PlanState.Load(fixture.Options.PlanFile).For("Debug");
        return (
            entries.Single(entry => entry.ProjectPath == FixtureRepository.LibraryProject),
            entries.Single(entry => entry.ProjectPath == FixtureRepository.TestProject));
    }
}
