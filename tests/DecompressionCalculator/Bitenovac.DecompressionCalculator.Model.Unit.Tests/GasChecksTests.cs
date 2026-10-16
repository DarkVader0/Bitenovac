using Bitenovac.DecompressionAlgorithms.Core.Equipment;

namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed class GasChecksTests
{
    private const int Precision = 6;
    private const double SurfaceMillibar = 1013.25;
    private const double MillibarPerMeter = 1030 * 9.80665 / 100;

    private static DiveResult Calculate(DiveInput dive) => PlanCalculator.Calculate(TestFactory.Plan(dive)).Dives[0];

    private static double Atmospheres(double meters) => (SurfaceMillibar + meters * MillibarPerMeter) / SurfaceMillibar;

    [Fact]
    public void GasChecks_ShouldGiveTheLevelAndEachGasAtItsDeepest_WhenTheDiveSwitchesGas()
    {
        // Arrange
        var dive = TestFactory.Dive(60, 20, cylinders:
        [
            TestFactory.Cylinder(18, 45, size: 24),
            TestFactory.Cylinder(50, 0, size: 11, purpose: CylinderPurpose.DecoGas)
        ]);

        // Act
        var checks = Calculate(dive).GasChecks;

        // Assert
        Assert.Equal(["TX18/45", "NX50"], checks.Select(static check => check.Gas));
        Assert.Equal(60, checks[0].Depth, Precision);
        Assert.Equal(21, checks[1].Depth, Precision);
        Assert.Equal(0.18 * Atmospheres(60) * SurfaceMillibar / 1000, checks[0].OxygenPressure, Precision);
        Assert.Equal((0.18 * GasMath.OxygenDensity + 0.37 * GasMath.NitrogenDensity + 0.45 * GasMath.HeliumDensity) * Atmospheres(60),
            checks[0].Density, Precision);
        Assert.Equal(GasMath.EquivalentNarcoticDepth(18, 45, 60, PlannerSettings.Default(UnitSystem.Metric), UnitSystem.Metric),
            checks[0].EquivalentNarcoticDepth, Precision);
    }

    [Fact]
    public void GasChecks_ShouldMarkTheGasAsTooDense_WhenAirIsBreathedDeep()
    {
        // Arrange
        var dive = TestFactory.Dive(50, 10, cylinders: [TestFactory.Cylinder(size: 24)]);

        // Act
        var check = Calculate(dive).GasChecks.Single();

        // Assert
        Assert.True(check.IsTooDense);
        Assert.Equal((0.21 * GasMath.OxygenDensity + 0.79 * GasMath.NitrogenDensity) * Atmospheres(50), check.Density, Precision);
    }

    [Fact]
    public void GasChecks_ShouldNotMarkTheGas_WhenItIsWithinTheDensityLimit()
    {
        // Arrange
        var dive = TestFactory.Dive(30, 20);

        // Act
        var check = Calculate(dive).GasChecks.Single();

        // Assert
        Assert.False(check.IsTooDense);
    }

    [Fact]
    public void Counterdiffusion_ShouldFlagTheSwitch_WhenNitrogenRisesByMoreThanAFifthOfTheHeliumDrop()
    {
        // Arrange
        var dive = TestFactory.Dive(60, 20, cylinders:
        [
            TestFactory.Cylinder(18, 45, size: 24),
            TestFactory.Cylinder(50, 0, size: 11, purpose: CylinderPurpose.DecoGas)
        ]);

        // Act
        var switches = Calculate(dive).Counterdiffusion;

        // Assert
        Assert.Equal([new CounterdiffusionRow("TX18/45", "NX50", 21)], switches);
    }

    [Fact]
    public void Counterdiffusion_ShouldFlagNothing_WhenTheNitrogenRiseIsWithinAFifthOfTheHeliumDrop()
    {
        // Arrange
        var dive = TestFactory.Dive(60, 20, cylinders:
        [
            TestFactory.Cylinder(18, 45, size: 24),
            TestFactory.Cylinder(35, 25, size: 11, purpose: CylinderPurpose.DecoGas)
        ]);

        // Act
        var result = Calculate(dive);

        // Assert
        Assert.Contains(result.GasChecks, static check => check.Gas == "TX35/25");
        Assert.Empty(result.Counterdiffusion);
    }

    [Fact]
    public void Counterdiffusion_ShouldFlagNothing_WhenTheSwitchGainsHelium()
    {
        // Arrange
        var dive = TestFactory.Dive(40, 20, cylinders:
        [
            TestFactory.Cylinder(size: 24),
            TestFactory.Cylinder(50, 10, size: 11, purpose: CylinderPurpose.DecoGas)
        ]);

        // Act
        var result = Calculate(dive);

        // Assert
        Assert.Contains(result.GasChecks, static check => check.Gas == "TX50/10");
        Assert.Empty(result.Counterdiffusion);
    }

    [Fact]
    public void GasChecks_ShouldUseTheLoopGas_WhenTheDiveIsOnARebreather()
    {
        // Arrange
        var dive = TestFactory.Dive(40, 20, circuit: new ClosedCircuitInput(1.3, 1.3),
            cylinders:
            [
                TestFactory.Cylinder(21, 35, size: 3, purpose: CylinderPurpose.Diluent),
                TestFactory.Cylinder(100, size: 3, purpose: CylinderPurpose.Oxygen)
            ]);

        // Act
        var check = Calculate(dive).GasChecks[0];

        // Assert
        Assert.Equal(1.3, check.OxygenPressure, Precision);
    }

    [Fact]
    public void Contingencies_ShouldCarryTheirOwnGasChecks_WhenTheDiveIsPlanned()
    {
        // Arrange
        var dive = TestFactory.Dive(60, 20, cylinders:
        [
            TestFactory.Cylinder(18, 45, size: 24),
            TestFactory.Cylinder(50, 0, size: 11, purpose: CylinderPurpose.DecoGas)
        ]);

        // Act
        var extended = Calculate(dive).Contingencies[0].Result;

        // Assert
        Assert.Equal(63, extended.GasChecks[0].Depth, Precision);
    }

    [Fact]
    public void GasChecks_ShouldCarryTheDensityLimitOfThePlan_WhenTheLimitIsChanged()
    {
        // Arrange
        var plan = TestFactory.Plan(TestFactory.Dive(30, 20)) with
        {
            Settings = PlannerSettings.Default(UnitSystem.Metric) with { GasDensityLimit = 9 }
        };

        // Act
        var check = PlanCalculator.Calculate(plan).Dives[0].GasChecks.Single();

        // Assert
        Assert.Equal(9, check.DensityLimit, Precision);
        Assert.False(check.IsTooDense);
    }
}
