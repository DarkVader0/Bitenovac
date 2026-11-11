using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Core.Scheduling;

namespace Bitenovac.RemoteBuildTool.Core.Unit.Tests;

public sealed class BuildUnitTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenTheProjectsAreNull()
    {
        // Arrange

        // Act
        var act = () => new BuildUnit(null!, TimeSpan.Zero, TimeSpan.Zero);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenThereAreNoProjects()
    {
        // Arrange
        ProjectId[] projects = [];

        // Act
        var act = () => new BuildUnit(projects, TimeSpan.Zero, TimeSpan.Zero);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Constructor_ShouldKeepItsOwnProjects_WhenTheCallersListChangesLater()
    {
        // Arrange
        var projects = new List<ProjectId> { TestFactory.Id("A") };
        var unit = new BuildUnit(projects, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));

        // Act
        projects.Add(TestFactory.Id("B"));

        // Assert
        Assert.Equal([TestFactory.Id("A")], unit.Projects);
        Assert.Equal(TimeSpan.FromSeconds(2), unit.Work);
        Assert.Equal(TimeSpan.FromSeconds(1), unit.CriticalPath);
    }

    [Fact]
    public void Constructor_ShouldHaveItsCriticalPathLeftAndNoDependencies_WhenNothingElseIsSaid()
    {
        // Arrange

        // Act
        var unit = new BuildUnit([TestFactory.Id("A")], TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(3));

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(3), unit.RemainingPath);
        Assert.Empty(unit.Dependencies);
    }

    [Fact]
    public void Constructor_ShouldKeepItsOwnDependencies_WhenTheCallersListChangesLater()
    {
        // Arrange
        var first = TestFactory.Unit("First", work: 1, criticalPath: 1);
        var dependencies = new List<BuildUnit> { first };
        var unit = new BuildUnit([TestFactory.Id("A")], TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), dependencies, TimeSpan.FromSeconds(9));

        // Act
        dependencies.Add(TestFactory.Unit("Second", work: 1, criticalPath: 1));

        // Assert
        Assert.Equal([first], unit.Dependencies);
        Assert.Equal(TimeSpan.FromSeconds(9), unit.RemainingPath);
    }
}
