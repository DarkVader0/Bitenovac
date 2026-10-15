using Bitenovac.DecompressionAlgorithms.Core.Equipment;

namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed class DivePlanDocumentTests
{
    private static DivePlanDocument Create(
        string name = "Plan",
        AlgorithmSettings? algorithm = null,
        PlannerSettings? settings = null,
        IReadOnlyList<DiveInput>? dives = null) =>
        new(TestFactory.PlanId, name, TestFactory.Now, TestFactory.Now, UnitSystem.Metric,
            algorithm ?? new BuhlmannZhl16cSettings(30, 70), settings ?? PlannerSettings.Default(UnitSystem.Metric),
            dives ?? [TestFactory.Dive()]);

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenNameIsNull()
    {
        // Arrange
        string name = null!;

        // Act
        Action act = () => Create(name: name);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenAlgorithmIsNull()
    {
        // Arrange
        AlgorithmSettings algorithm = null!;

        // Act
        Action act = () => _ = new DivePlanDocument(TestFactory.PlanId, "Plan", TestFactory.Now, TestFactory.Now,
            UnitSystem.Metric, algorithm, PlannerSettings.Default(UnitSystem.Metric), [TestFactory.Dive()]);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenSettingsIsNull()
    {
        // Arrange
        PlannerSettings settings = null!;

        // Act
        Action act = () => _ = new DivePlanDocument(TestFactory.PlanId, "Plan", TestFactory.Now, TestFactory.Now,
            UnitSystem.Metric, new BuhlmannZhl16cSettings(30, 70), settings, [TestFactory.Dive()]);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenDivesIsNull()
    {
        // Arrange
        IReadOnlyList<DiveInput> dives = null!;

        // Act
        Action act = () => _ = new DivePlanDocument(TestFactory.PlanId, "Plan", TestFactory.Now, TestFactory.Now,
            UnitSystem.Metric, new BuhlmannZhl16cSettings(30, 70), PlannerSettings.Default(UnitSystem.Metric), dives);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Constructor_ShouldThrowArgumentException_WhenTheCreationTimeIsNotUtc(DateTimeKind kind)
    {
        // Arrange
        var createdAt = DateTime.SpecifyKind(TestFactory.Now, kind);

        // Act
        Action act = () => _ = new DivePlanDocument(TestFactory.PlanId, "Plan", createdAt, TestFactory.Now,
            UnitSystem.Metric, new BuhlmannZhl16cSettings(30, 70), PlannerSettings.Default(UnitSystem.Metric), [TestFactory.Dive()]);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void ModifiedAt_ShouldThrowArgumentException_WhenSetToATimeNotInUtc(DateTimeKind kind)
    {
        // Arrange
        var plan = Create();
        var modifiedAt = DateTime.SpecifyKind(TestFactory.Now, kind);

        // Act
        Action act = () => _ = plan with { ModifiedAt = modifiedAt };

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Constructor_ShouldKeepItsOwnCopy_WhenTheCallerChangesItsDives()
    {
        // Arrange
        var dives = new List<DiveInput> { TestFactory.Dive() };
        var plan = Create(dives: dives);

        // Act
        dives.Add(TestFactory.Dive(surfaceIntervalMinutes: 60));

        // Assert
        Assert.Single(plan.Dives);
    }

    [Fact]
    public void CreateNew_ShouldPlanOneMetricAirDive_WhenTheUnitsAreMetric()
    {
        // Arrange

        // Act
        var plan = DivePlanDocument.CreateNew(TestFactory.PlanId, "New", UnitSystem.Metric, TestFactory.Now);

        // Assert
        Assert.Equal(TestFactory.PlanId, plan.Id);
        Assert.Equal("New", plan.Name);
        Assert.Equal(TestFactory.Now, plan.CreatedAt);
        Assert.Equal(TestFactory.Now, plan.ModifiedAt);
        Assert.Equal(UnitSystem.Metric, plan.Units);
        Assert.Equal(AlgorithmCatalog.Algorithms[0].DefaultSettings, plan.Algorithm);
        Assert.Equal(PlannerSettings.Default(UnitSystem.Metric), plan.Settings);
        var dive = Assert.Single(plan.Dives);
        Assert.Equal(new OpenCircuitInput(), dive.Circuit);
        Assert.Equal(new LevelInput(30, 20), Assert.Single(dive.Levels));
        Assert.Equal(new CylinderInput(21, 0, 12, 0, 200, CylinderPurpose.BottomGas), Assert.Single(dive.Cylinders));
    }

    [Fact]
    public void CreateNew_ShouldPlanOneImperialAirDiveOnAnAluminum80_WhenTheUnitsAreImperial()
    {
        // Arrange

        // Act
        var plan = DivePlanDocument.CreateNew(TestFactory.PlanId, "New", UnitSystem.Imperial, TestFactory.Now);

        // Assert
        Assert.Equal(UnitSystem.Imperial, plan.Units);
        Assert.Equal(PlannerSettings.Default(UnitSystem.Imperial), plan.Settings);
        var dive = Assert.Single(plan.Dives);
        Assert.Equal(new LevelInput(100, 20), Assert.Single(dive.Levels));
        Assert.Equal(new CylinderInput(21, 0, 80, 3000, 3000, CylinderPurpose.BottomGas), Assert.Single(dive.Cylinders));
    }

    [Fact]
    public void Dives_ShouldThrowArgumentNullException_WhenSetToNull()
    {
        // Arrange
        var plan = Create();

        // Act
        Action act = () => _ = plan with { Dives = null! };

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Equals_ShouldBeTrue_WhenEveryValueAndDiveIsEqual()
    {
        // Arrange
        var first = Create();
        var second = Create();

        // Act
        var equal = first.Equals(second);

        // Assert
        Assert.True(equal);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    public static TheoryData<DivePlanDocument> DifferentPlans()
    {
        var plan = Create();
        return
        [
            plan with { Id = Guid.Empty },
            plan with { Name = "Other" },
            plan with { CreatedAt = TestFactory.Now.AddDays(-1) },
            plan with { ModifiedAt = TestFactory.Now.AddDays(1) },
            plan with { Units = UnitSystem.Imperial },
            plan with { Algorithm = new BuhlmannZhl16cSettings(40, 85) },
            plan with { Settings = PlannerSettings.Default(UnitSystem.Imperial) },
            plan with { Dives = [TestFactory.Dive(depth: 18)] }
        ];
    }

    [Theory]
    [MemberData(nameof(DifferentPlans))]
    public void Equals_ShouldBeFalse_WhenAnyValueDiffers(DivePlanDocument other)
    {
        // Arrange
        var plan = Create();

        // Act
        var equal = plan.Equals(other);

        // Assert
        Assert.False(equal);
    }

    [Fact]
    public void Equals_ShouldBeFalse_WhenTheOtherPlanIsNull()
    {
        // Arrange
        var plan = Create();

        // Act
        var equal = plan.Equals(null);

        // Assert
        Assert.False(equal);
    }
}
