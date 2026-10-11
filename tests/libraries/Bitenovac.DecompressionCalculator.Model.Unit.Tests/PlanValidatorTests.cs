using Bitenovac.DecompressionAlgorithms.Core.Environment;
using Bitenovac.DecompressionAlgorithms.Core.Equipment;
using Bitenovac.DecompressionAlgorithms.Core.Planning;

namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed class PlanValidatorTests
{
    private sealed record UnknownAlgorithmSettings : AlgorithmSettings;

    private sealed record UnknownCircuitInput : CircuitInput;

    private static DivePlanDocument WithSettings(Func<PlannerSettings, PlannerSettings> change)
    {
        var plan = TestFactory.Plan();
        return plan with { Settings = change(plan.Settings) };
    }

    private static DivePlanDocument WithCylinders(CircuitInput circuit, params CylinderInput[] cylinders) =>
        TestFactory.Plan(TestFactory.Dive(circuit: circuit, cylinders: cylinders));

    [Fact]
    public void Validate_ShouldThrowArgumentNullException_WhenThePlanIsNull()
    {
        // Arrange
        DivePlanDocument plan = null!;

        // Act
        Action act = () => PlanValidator.Validate(plan);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Theory]
    [InlineData(UnitSystem.Metric)]
    [InlineData(UnitSystem.Imperial)]
    public void Validate_ShouldFindNoIssues_WhenTheNewPlanIsUnchanged(UnitSystem units)
    {
        // Arrange
        var plan = DivePlanDocument.CreateNew(TestFactory.PlanId, "Plan", units, TestFactory.Now);

        // Act
        var issues = PlanValidator.Validate(plan);

        // Assert
        Assert.Empty(issues);
    }

    public static TheoryData<DivePlanDocument, string> InvalidPlans()
    {
        var plan = TestFactory.Plan();
        var imperial = DivePlanDocument.CreateNew(TestFactory.PlanId, "Plan", UnitSystem.Imperial, TestFactory.Now);
        var closed = new ClosedCircuitInput(1.3, 1.3);
        var semiClosed = new SemiClosedCircuitInput(0.1);
        var diluent = TestFactory.Cylinder(purpose: CylinderPurpose.Diluent);
        var oxygen = TestFactory.Cylinder(oxygenPercent: 100, size: 3, purpose: CylinderPurpose.Oxygen);

        return new TheoryData<DivePlanDocument, string>
        {
            { plan with { Algorithm = new BuhlmannZhl16cSettings(0, 70) }, "Algorithm.GradientFactorLow" },
            { plan with { Algorithm = new BuhlmannZhl16cSettings(30, 101) }, "Algorithm.GradientFactorHigh" },
            { plan with { Algorithm = new BuhlmannZhl16cSettings(80, 70) }, "Algorithm.GradientFactorLow" },
            { plan with { Algorithm = new UnknownAlgorithmSettings() }, "Algorithm" },
            { WithSettings(s => s with { Altitude = double.NaN }), "Settings.Altitude" },
            { WithSettings(s => s with { Altitude = -501 }), "Settings.Altitude" },
            { WithSettings(s => s with { Altitude = 6001 }), "Settings.Altitude" },
            { imperial with { Settings = imperial.Settings with { Altitude = 20000 } }, "Settings.Altitude" },
            { WithSettings(s => s with { Salinity = (Salinity)99 }), "Settings.Salinity" },
            { WithSettings(s => s with { DescentRate = 0 }), "Settings.DescentRate" },
            { WithSettings(s => s with { DescentRate = double.PositiveInfinity }), "Settings.DescentRate" },
            { WithSettings(s => s with { AscentRateDeep = -9 }), "Settings.AscentRateDeep" },
            { WithSettings(s => s with { AscentRateMiddle = 0 }), "Settings.AscentRateMiddle" },
            { WithSettings(s => s with { AscentRateToStops = 0 }), "Settings.AscentRateToStops" },
            { WithSettings(s => s with { AscentRateShallow = 0 }), "Settings.AscentRateShallow" },
            { WithSettings(s => s with { BottomSac = 0 }), "Settings.BottomSac" },
            { WithSettings(s => s with { DecoSac = double.NaN }), "Settings.DecoSac" },
            { WithSettings(s => s with { BottomMetabolicOxygen = -1 }), "Settings.BottomMetabolicOxygen" },
            { WithSettings(s => s with { DecoMetabolicOxygen = double.PositiveInfinity }), "Settings.DecoMetabolicOxygen" },
            { WithSettings(s => s with { LoopVolume = -6 }), "Settings.LoopVolume" },
            { WithSettings(s => s with { BottomPo2 = 0 }), "Settings.BottomPo2" },
            { WithSettings(s => s with { DecoPo2 = 0 }), "Settings.DecoPo2" },
            { WithSettings(s => s with { MaximumOperatingDepthModel = (MaximumOperatingDepthModel)99 }), "Settings.MaximumOperatingDepthModel" },
            { WithSettings(s => s with { ReserveStressFactor = 0.9 }), "Settings.ReserveStressFactor" },
            { WithSettings(s => s with { ReserveStressFactor = double.PositiveInfinity }), "Settings.ReserveStressFactor" },
            { WithSettings(s => s with { ReserveTeamSize = 0 }), "Settings.ReserveTeamSize" },
            { WithSettings(s => s with { StopTimeIncrementMinutes = 0 }), "Settings.StopTimeIncrementMinutes" },
            { WithSettings(s => s with { GasSwitchMinutes = -1 }), "Settings.GasSwitchMinutes" },
            { WithSettings(s => s with { OxygenBreaks = true, OxygenBreakIntervalMinutes = 0 }), "Settings.OxygenBreakIntervalMinutes" },
            { WithSettings(s => s with { OxygenBreaks = true, OxygenBreakDurationMinutes = 0 }), "Settings.OxygenBreakDurationMinutes" },
            { WithSettings(s => s with { OxygenBreakIntervalMinutes = -1 }), "Settings.OxygenBreakIntervalMinutes" },
            { WithSettings(s => s with { OxygenBreakDurationMinutes = -1 }), "Settings.OxygenBreakDurationMinutes" },
            { plan with { Dives = [] }, "Dives" },
            { TestFactory.Plan(TestFactory.Dive(), TestFactory.Dive(surfaceIntervalMinutes: 0)), "Dives[1].SurfaceIntervalMinutes" },
            { TestFactory.Plan(TestFactory.Dive(circuit: closed with { BottomSetpoint = 0 }, cylinders: [diluent, oxygen])), "Dives[0].Circuit.BottomSetpoint" },
            { TestFactory.Plan(TestFactory.Dive(circuit: closed with { DecoSetpoint = 0 }, cylinders: [diluent, oxygen])), "Dives[0].Circuit.DecoSetpoint" },
            { TestFactory.Plan(TestFactory.Dive(circuit: semiClosed with { DumpRatio = 0 }, cylinders: [diluent])), "Dives[0].Circuit.DumpRatio" },
            { TestFactory.Plan(TestFactory.Dive(circuit: semiClosed with { DumpRatio = 1.5 }, cylinders: [diluent])), "Dives[0].Circuit.DumpRatio" },
            { TestFactory.Plan(TestFactory.Dive(circuit: new UnknownCircuitInput())), "Dives[0].Circuit" },
            { TestFactory.Plan(TestFactory.Dive() with { Levels = [] }), "Dives[0].Levels" },
            { TestFactory.Plan(TestFactory.Dive(depth: 0)), "Dives[0].Levels[0].Depth" },
            { TestFactory.Plan(TestFactory.Dive(minutes: -1)), "Dives[0].Levels[0].DurationMinutes" },
            { TestFactory.Plan(TestFactory.Dive(cylinders: [])), "Dives[0].Cylinders" },
            { TestFactory.Plan(TestFactory.Dive(cylinders: [TestFactory.Cylinder(oxygenPercent: 0)])), "Dives[0].Cylinders[0].OxygenPercent" },
            { TestFactory.Plan(TestFactory.Dive(cylinders: [TestFactory.Cylinder(oxygenPercent: 101)])), "Dives[0].Cylinders[0].OxygenPercent" },
            { TestFactory.Plan(TestFactory.Dive(cylinders: [TestFactory.Cylinder(heliumPercent: -1)])), "Dives[0].Cylinders[0].HeliumPercent" },
            { TestFactory.Plan(TestFactory.Dive(cylinders: [TestFactory.Cylinder(heliumPercent: 101)])), "Dives[0].Cylinders[0].HeliumPercent" },
            { TestFactory.Plan(TestFactory.Dive(cylinders: [TestFactory.Cylinder(oxygenPercent: 50, heliumPercent: 60)])), "Dives[0].Cylinders[0].HeliumPercent" },
            { TestFactory.Plan(TestFactory.Dive(cylinders: [TestFactory.Cylinder(size: 0)])), "Dives[0].Cylinders[0].Size" },
            { imperial with { Dives = [imperial.Dives[0] with { Cylinders = [TestFactory.Cylinder(size: 80, ratedPressure: 0, startPressure: 3000)] }] }, "Dives[0].Cylinders[0].RatedPressure" },
            { TestFactory.Plan(TestFactory.Dive(cylinders: [TestFactory.Cylinder(startPressure: 0)])), "Dives[0].Cylinders[0].StartPressure" },
            { TestFactory.Plan(TestFactory.Dive(cylinders: [TestFactory.Cylinder(purpose: (CylinderPurpose)99)])), "Dives[0].Cylinders[0].Purpose" },
            { WithCylinders(closed, oxygen), "Dives[0].Cylinders" },
            { WithCylinders(closed, diluent), "Dives[0].Cylinders" },
            { WithCylinders(semiClosed, TestFactory.Cylinder()), "Dives[0].Cylinders" }
        };
    }

    [Theory]
    [MemberData(nameof(InvalidPlans))]
    public void Validate_ShouldReportTheField_WhenItsValueCannotBePlannedWith(DivePlanDocument plan, string path)
    {
        // Arrange

        // Act
        var issues = PlanValidator.Validate(plan);

        // Assert
        var issue = Assert.Single(issues);
        Assert.Equal(path, issue.Path);
        Assert.False(string.IsNullOrWhiteSpace(issue.Message));
    }

    public static TheoryData<DivePlanDocument> ValidEdgePlans()
    {
        var closed = new ClosedCircuitInput(1.3, 1.3);
        var semiClosed = new SemiClosedCircuitInput(1);
        var diluent = TestFactory.Cylinder(purpose: CylinderPurpose.Diluent);
        var oxygen = TestFactory.Cylinder(oxygenPercent: 100, size: 3, purpose: CylinderPurpose.Oxygen);

        return
        [
            TestFactory.Plan() with { Algorithm = new BuhlmannZhl16cSettings(100, 100) },
            WithSettings(s => s with { Altitude = -500 }),
            WithSettings(s => s with { Altitude = 6000 }),
            WithSettings(s => s with { OxygenBreaks = true }),
            WithSettings(s => s with { BottomMetabolicOxygen = 0, LoopVolume = 0, GasSwitchMinutes = 0 }),
            TestFactory.Plan(TestFactory.Dive(surfaceIntervalMinutes: -10)),
            TestFactory.Plan(TestFactory.Dive(minutes: 0)),
            TestFactory.Plan(TestFactory.Dive(cylinders: [TestFactory.Cylinder(oxygenPercent: 10, heliumPercent: 90)])),
            WithCylinders(closed, diluent, oxygen),
            WithCylinders(semiClosed, diluent)
        ];
    }

    [Theory]
    [MemberData(nameof(ValidEdgePlans))]
    public void Validate_ShouldFindNoIssues_WhenValuesSitOnTheirLimits(DivePlanDocument plan)
    {
        // Arrange

        // Act
        var issues = PlanValidator.Validate(plan);

        // Assert
        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_ShouldReportEveryIssue_WhenSeveralFieldsAreWrong()
    {
        // Arrange
        var plan = TestFactory.Plan(TestFactory.Dive(depth: 0, cylinders: [TestFactory.Cylinder(size: 0)])) with
        {
            Algorithm = new BuhlmannZhl16cSettings(0, 0)
        };

        // Act
        var issues = PlanValidator.Validate(plan);

        // Assert
        Assert.Equal(
            ["Algorithm.GradientFactorLow", "Algorithm.GradientFactorHigh", "Dives[0].Levels[0].Depth", "Dives[0].Cylinders[0].Size"],
            issues.Select(static issue => issue.Path));
    }
}
