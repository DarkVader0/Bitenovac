using Bitenovac.DecompressionAlgorithms.Core.Equipment;
using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed class PlanMapperTests
{
    private const int Precision = 9;
    private const double MetersPerFoot = 0.3048;
    private const double LitersPerCubicFoot = 28.316846592;

    private sealed record UnknownCircuitInput : CircuitInput;

    [Fact]
    public void ToSettings_ShouldCarryEveryEnteredValue_WhenTheUnitsAreMetric()
    {
        // Arrange
        var entered = PlannerSettings.Default(UnitSystem.Metric) with
        {
            ReserveStressFactor = 1.5,
            ReserveTeamSize = 3,
            SafetyStop = true,
            OxygenBreaks = true,
            LastStopAtSixMeters = true,
            SwitchAtRequiredStop = true
        };

        // Act
        var settings = PlanMapper.ToSettings(entered, UnitSystem.Metric);

        // Assert
        Assert.Equal(UnitConversion.SurfacePressure(0, UnitSystem.Metric), settings.SurfacePressure);
        Assert.Equal(entered.Salinity, settings.Salinity);
        Assert.Equal(18, settings.DescentRateMetersPerMinute, Precision);
        Assert.Equal(9, settings.AscentRateBelow75PercentMetersPerMinute, Precision);
        Assert.Equal(9, settings.AscentRate75To50PercentMetersPerMinute, Precision);
        Assert.Equal(9, settings.AscentRate50PercentToStopsMetersPerMinute, Precision);
        Assert.Equal(1, settings.AscentRateLastSixMetersMetersPerMinute, Precision);
        Assert.Equal(18, settings.BottomSacLitersPerMinute, Precision);
        Assert.Equal(14, settings.DecoSacLitersPerMinute, Precision);
        Assert.Equal(1, settings.BottomMetabolicOxygenConsumptionLitersPerMinute, Precision);
        Assert.Equal(0.6, settings.DecoMetabolicOxygenConsumptionLitersPerMinute, Precision);
        Assert.Equal(6, settings.LoopVolumeLiters, Precision);
        Assert.Equal(1.4, settings.BottomPo2.InBar, Precision);
        Assert.Equal(1.6, settings.DecoPo2.InBar, Precision);
        Assert.Equal(entered.MaximumOperatingDepthModel, settings.MaximumOperatingDepthModel);
        Assert.Equal(Pressure.Zero, settings.ReservePressure);
        Assert.Equal(1.5, settings.ReserveStressFactor, Precision);
        Assert.Equal(3, settings.ReserveTeamSize);
        Assert.Equal(TimeSpan.FromMinutes(1), settings.StopTimeIncrement);
        Assert.Equal(TimeSpan.Zero, settings.ProblemSolvingTime);
        Assert.Equal(TimeSpan.FromMinutes(2), settings.MinimumGasSwitchDuration);
        Assert.Equal(TimeSpan.FromMinutes(20), settings.OxygenBreakInterval);
        Assert.Equal(TimeSpan.FromMinutes(5), settings.OxygenBreakDuration);
        Assert.True(settings.SafetyStop);
        Assert.True(settings.LastStopAtSixMeters);
        Assert.True(settings.SwitchAtRequiredStop);
        Assert.True(settings.OxygenBreaks);
        Assert.False(settings.OxygenIsNarcotic);
    }

    [Fact]
    public void ToSettings_ShouldConvertFeetAndCubicFeet_WhenTheUnitsAreImperial()
    {
        // Arrange
        var entered = PlannerSettings.Default(UnitSystem.Imperial) with { Altitude = 1000 };

        // Act
        var settings = PlanMapper.ToSettings(entered, UnitSystem.Imperial);

        // Assert
        Assert.Equal(UnitConversion.SurfacePressure(1000, UnitSystem.Imperial), settings.SurfacePressure);
        Assert.Equal(60 * MetersPerFoot, settings.DescentRateMetersPerMinute, Precision);
        Assert.Equal(30 * MetersPerFoot, settings.AscentRateBelow75PercentMetersPerMinute, Precision);
        Assert.Equal(30 * MetersPerFoot, settings.AscentRate75To50PercentMetersPerMinute, Precision);
        Assert.Equal(30 * MetersPerFoot, settings.AscentRate50PercentToStopsMetersPerMinute, Precision);
        Assert.Equal(3 * MetersPerFoot, settings.AscentRateLastSixMetersMetersPerMinute, Precision);
        Assert.Equal(0.65 * LitersPerCubicFoot, settings.BottomSacLitersPerMinute, Precision);
        Assert.Equal(0.5 * LitersPerCubicFoot, settings.DecoSacLitersPerMinute, Precision);
        Assert.Equal(1, settings.BottomMetabolicOxygenConsumptionLitersPerMinute, Precision);
        Assert.Equal(6, settings.LoopVolumeLiters, Precision);
    }

    [Fact]
    public void ToCylinder_ShouldBuildTheGasSizeFillAndPurpose_WhenTheUnitsAreMetric()
    {
        // Arrange
        var entered = TestFactory.Cylinder(oxygenPercent: 18, heliumPercent: 45, size: 24, startPressure: 232,
            purpose: CylinderPurpose.Diluent);

        // Act
        var cylinder = PlanMapper.ToCylinder(entered, UnitSystem.Metric);

        // Assert
        Assert.Equal(GasMixture.FromPercent(18, 45), cylinder.Gas);
        Assert.Equal(24, cylinder.Size.InLiter, Precision);
        Assert.Equal(232, cylinder.StartPressure.InBar, Precision);
        Assert.Equal(CylinderPurpose.Diluent, cylinder.Purpose);
    }

    [Fact]
    public void ToCylinder_ShouldReadPsiAndRatedVolume_WhenTheUnitsAreImperial()
    {
        // Arrange
        var entered = TestFactory.Cylinder(size: 80, ratedPressure: 3000, startPressure: 2800);

        // Act
        var cylinder = PlanMapper.ToCylinder(entered, UnitSystem.Imperial);

        // Assert
        Assert.Equal(UnitConversion.CylinderWaterVolume(entered, UnitSystem.Imperial), cylinder.Size);
        Assert.Equal(Pressure.FromPsi(2800), cylinder.StartPressure);
    }

    [Fact]
    public void ToLoop_ShouldBeOpenCircuit_WhenTheApparatusIsOpenCircuit()
    {
        // Arrange
        var settings = PlanMapper.ToSettings(PlannerSettings.Default(UnitSystem.Metric), UnitSystem.Metric);

        // Act
        var loop = PlanMapper.ToLoop(new OpenCircuitInput(), settings);

        // Assert
        Assert.Equal(BreathingLoop.OpenCircuit, loop);
    }

    [Fact]
    public void ToLoop_ShouldHoldBothSetpoints_WhenTheApparatusIsAClosedCircuitRebreather()
    {
        // Arrange
        var settings = PlanMapper.ToSettings(PlannerSettings.Default(UnitSystem.Metric), UnitSystem.Metric);
        var circuit = new ClosedCircuitInput(1.2, 1.4);

        // Act
        var loop = PlanMapper.ToLoop(circuit, settings);

        // Assert
        Assert.Equal(BreathingLoop.ClosedCircuit(Pressure.FromBar(1.2), Pressure.FromBar(1.4)), loop);
    }

    [Fact]
    public void ToLoop_ShouldDeriveTheOxygenDropFromTheBottomBreathingRate_WhenTheApparatusIsASemiClosedRebreather()
    {
        // Arrange
        var settings = PlanMapper.ToSettings(PlannerSettings.Default(UnitSystem.Metric), UnitSystem.Metric);
        var circuit = new SemiClosedCircuitInput(0.1);
        var expectedDropMillibar = 1 * settings.SurfacePressure.InMillibar / (0.1 * 18);

        // Act
        var loop = PlanMapper.ToLoop(circuit, settings);

        // Assert
        Assert.Equal(DiveMode.PSCR, loop.Mode);
        Assert.Equal(0.1, loop.DumpRatio, Precision);
        Assert.Equal(expectedDropMillibar, loop.OxygenDropCoefficient.InMillibar, Precision);
    }

    [Fact]
    public void ToLoop_ShouldThrowArgumentOutOfRangeException_WhenTheApparatusIsUnknown()
    {
        // Arrange
        var settings = PlanMapper.ToSettings(PlannerSettings.Default(UnitSystem.Metric), UnitSystem.Metric);
        var circuit = new UnknownCircuitInput();

        // Act
        Action act = () => PlanMapper.ToLoop(circuit, settings);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void ToProfile_ShouldTurnEveryLevelIntoABottomSegmentOnTheLoop_WhenTheDiveHasLevels()
    {
        // Arrange
        var dive = TestFactory.Dive() with { Levels = [new LevelInput(100, 15), new LevelInput(60, 10)] };
        Cylinder[] cylinders = [PlanMapper.ToCylinder(TestFactory.Cylinder(oxygenPercent: 32), UnitSystem.Imperial)];
        var loop = BreathingLoop.ClosedCircuit(Pressure.FromBar(1.3));

        // Act
        var profile = PlanMapper.ToProfile(dive, cylinders, loop, UnitSystem.Imperial);

        // Assert
        Assert.Collection(profile.Segments,
            segment =>
            {
                Assert.Equal(100 * MetersPerFoot, segment.Depth.InMeter, Precision);
                Assert.Equal(TimeSpan.FromMinutes(15), segment.Duration);
                Assert.Equal(GasMixture.FromPercent(32, 0), segment.Gas);
                Assert.Equal(SegmentKind.Bottom, segment.Kind);
                Assert.Equal(loop, segment.Loop);
            },
            segment =>
            {
                Assert.Equal(60 * MetersPerFoot, segment.Depth.InMeter, Precision);
                Assert.Equal(TimeSpan.FromMinutes(10), segment.Duration);
            });
    }
}
