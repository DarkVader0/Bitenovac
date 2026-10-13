namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed class DiveInputTests
{
    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenCircuitIsNull()
    {
        // Arrange
        CircuitInput circuit = null!;

        // Act
        Action act = () => new DiveInput(0, circuit, [new LevelInput(30, 20)], [TestFactory.Cylinder()]);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenLevelsIsNull()
    {
        // Arrange
        IReadOnlyList<LevelInput> levels = null!;

        // Act
        Action act = () => new DiveInput(0, new OpenCircuitInput(), levels, [TestFactory.Cylinder()]);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenCylindersIsNull()
    {
        // Arrange
        IReadOnlyList<CylinderInput> cylinders = null!;

        // Act
        Action act = () => new DiveInput(0, new OpenCircuitInput(), [new LevelInput(30, 20)], cylinders);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_ShouldKeepItsOwnCopies_WhenTheCallerChangesItsLists()
    {
        // Arrange
        var levels = new List<LevelInput> { new(30, 20) };
        var cylinders = new List<CylinderInput> { TestFactory.Cylinder() };
        var dive = new DiveInput(0, new OpenCircuitInput(), levels, cylinders);

        // Act
        levels.Add(new LevelInput(10, 5));
        cylinders.Clear();

        // Assert
        Assert.Single(dive.Levels);
        Assert.Single(dive.Cylinders);
    }

    [Fact]
    public void Levels_ShouldKeepItsOwnCopy_WhenSetThroughWith()
    {
        // Arrange
        var levels = new List<LevelInput> { new(40, 10), new(20, 10) };
        var dive = TestFactory.Dive() with { Levels = levels };

        // Act
        levels.Clear();

        // Assert
        Assert.Equal(2, dive.Levels.Count);
    }

    [Fact]
    public void Levels_ShouldThrowArgumentNullException_WhenSetToNull()
    {
        // Arrange
        var dive = TestFactory.Dive();

        // Act
        Action act = () => _ = dive with { Levels = null! };

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Cylinders_ShouldThrowArgumentNullException_WhenSetToNull()
    {
        // Arrange
        var dive = TestFactory.Dive();

        // Act
        Action act = () => _ = dive with { Cylinders = null! };

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Equals_ShouldBeTrue_WhenEveryValueLevelAndCylinderIsEqual()
    {
        // Arrange
        var first = TestFactory.Dive(surfaceIntervalMinutes: 60);
        var second = TestFactory.Dive(surfaceIntervalMinutes: 60);

        // Act
        var equal = first.Equals(second);

        // Assert
        Assert.True(equal);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    public static TheoryData<DiveInput> DifferentDives() =>
    [
        TestFactory.Dive(surfaceIntervalMinutes: 30),
        TestFactory.Dive() with { Circuit = new ClosedCircuitInput(1.3, 1.3) },
        TestFactory.Dive(depth: 40),
        TestFactory.Dive(cylinders: [TestFactory.Cylinder(oxygenPercent: 32)]),
        TestFactory.Dive() with { Name = "Reef" }
    ];

    [Theory]
    [MemberData(nameof(DifferentDives))]
    public void Equals_ShouldBeFalse_WhenAnyValueDiffers(DiveInput other)
    {
        // Arrange
        var dive = TestFactory.Dive();

        // Act
        var equal = dive.Equals(other);

        // Assert
        Assert.False(equal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DisplayName_ShouldBeDiveAndItsNumber_WhenTheDiveHasNoName(string? name)
    {
        // Arrange
        var dive = TestFactory.Dive() with { Name = name };

        // Act
        var shown = dive.DisplayName(2);

        // Assert
        Assert.Equal("Dive 2", shown);
    }

    [Fact]
    public void DisplayName_ShouldBeTheTrimmedName_WhenTheDiveHasOne()
    {
        // Arrange
        var dive = TestFactory.Dive() with { Name = "  Wreck  " };

        // Act
        var shown = dive.DisplayName(2);

        // Assert
        Assert.Equal("Wreck", shown);
    }

    [Fact]
    public void Equals_ShouldBeFalse_WhenTheOtherDiveIsNull()
    {
        // Arrange
        var dive = TestFactory.Dive();

        // Act
        var equal = dive.Equals(null);

        // Assert
        Assert.False(equal);
    }
}
