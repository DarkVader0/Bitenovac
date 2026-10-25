using Bitenovac.DecompressionAlgorithms.Core.Abstractions;
using Bitenovac.DecompressionAlgorithms.Core.Equipment;
using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionAlgorithms.Core.Unit.Tests;

public sealed class DivePlannerTests
{
    private const int Precision = 5;

    private static readonly GasMixture Nitrox50 = GasMixture.FromPercent(50, 0);

    private static DivePlanRequest CreateRequest(IEnumerable<DiveSegment> profile,
        IEnumerable<Cylinder>? cylinders = null) =>
        new(new DiveProfile(profile),
            cylinders ?? [TestFactory.CreateCylinder(GasMixture.Air)],
            TestFactory.CreateSettings());

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenAlgorithmIsNull()
    {
        // Arrange
        IDecompressionAlgorithm algorithm = null!;

        // Act
        Action act = () => new DivePlanner(algorithm);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void CreatePlan_ShouldThrowArgumentNullException_WhenRequestIsNull()
    {
        // Arrange
        var planner = new DivePlanner(new FakeAlgorithm());

        // Act
        Action act = () => planner.CreatePlan(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void CreatePlan_ShouldBeginTheDiveOnce_WhenAPlanIsCreated()
    {
        // Arrange
        var algorithm = new FakeAlgorithm();
        var planner = new DivePlanner(algorithm);
        var request = CreateRequest([TestFactory.CreateSegment(30, 10)]);

        // Act
        planner.CreatePlan(request);

        // Assert
        Assert.Equal(1, algorithm.BeginDiveCallCount);
    }

    [Fact]
    public void CreatePlan_ShouldBuildADescentAndABottomSegment_WhenTheProfileHasASingleLevel()
    {
        // Arrange
        var planner = new DivePlanner(new FakeAlgorithm());
        var request = CreateRequest([TestFactory.CreateSegment(30, 10)]);

        // Act
        var plan = planner.CreatePlan(request);

        // Assert
        Assert.Equal(2, plan.ExpandedSegments.Count);
        Assert.Equal(SegmentKind.Descent, plan.ExpandedSegments[0].Kind);
        Assert.Equal(SegmentKind.Bottom, plan.ExpandedSegments[1].Kind);
    }

    [Fact]
    public void CreatePlan_ShouldTimeTheDescentAtTheDescentRate_WhenDescendingToTheFirstLevel()
    {
        // Arrange
        var planner = new DivePlanner(new FakeAlgorithm());
        var request = CreateRequest([TestFactory.CreateSegment(30, 10)]);

        // Act
        var plan = planner.CreatePlan(request);

        // Assert
        Assert.Equal(1.5, plan.ExpandedSegments[0].Duration.TotalMinutes, Precision);
        Assert.Equal(30, plan.ExpandedSegments[0].Depth.InMeter, Precision);
    }

    [Fact]
    public void CreatePlan_ShouldTimeTheAscentAtTheWorkingAscentRate_WhenMovingToAShallowerLevel()
    {
        // Arrange
        var planner = new DivePlanner(new FakeAlgorithm());
        var request = CreateRequest([
            TestFactory.CreateSegment(30, 10),
            TestFactory.CreateSegment(20, 5)
        ]);

        // Act
        var plan = planner.CreatePlan(request);

        // Assert
        Assert.Equal(SegmentKind.Ascent, plan.ExpandedSegments[2].Kind);
        Assert.Equal(1, plan.ExpandedSegments[2].Duration.TotalMinutes, Precision);
    }

    [Fact]
    public void CreatePlan_ShouldOmitTheBottomSegment_WhenTheTargetHasNoDuration()
    {
        // Arrange
        var planner = new DivePlanner(new FakeAlgorithm());
        var request = CreateRequest([TestFactory.CreateSegment(30, 0)]);

        // Act
        var plan = planner.CreatePlan(request);

        // Assert
        Assert.Single(plan.ExpandedSegments);
        Assert.Equal(SegmentKind.Descent, plan.ExpandedSegments[0].Kind);
    }

    [Fact]
    public void CreatePlan_ShouldOmitTheTravelSegment_WhenTheNextLevelIsAtTheSameDepth()
    {
        // Arrange
        var planner = new DivePlanner(new FakeAlgorithm());
        var request = CreateRequest([
            TestFactory.CreateSegment(30, 10),
            TestFactory.CreateSegment(30, 5)
        ]);

        // Act
        var plan = planner.CreatePlan(request);

        // Assert
        Assert.Equal(3, plan.ExpandedSegments.Count);
        Assert.Equal(SegmentKind.Bottom, plan.ExpandedSegments[1].Kind);
        Assert.Equal(SegmentKind.Bottom, plan.ExpandedSegments[2].Kind);
    }

    [Fact]
    public void CreatePlan_ShouldLoadEveryWorkingSegmentIntoTheModel_WhenTheProfileIsWalked()
    {
        // Arrange
        var algorithm = new FakeAlgorithm();
        var planner = new DivePlanner(algorithm);
        var request = CreateRequest([
            TestFactory.CreateSegment(30, 10),
            TestFactory.CreateSegment(20, 5)
        ]);

        // Act
        planner.CreatePlan(request);

        // Assert
        Assert.Equal(4, algorithm.LoadedSegments.Count);
    }

    [Fact]
    public void CreatePlan_ShouldPlanTheInterLevelAscentThroughTheModel_WhenTheCeilingIsDeeperThanTheTarget()
    {
        // Arrange
        var stop = new DiveSegment(Depth.FromMeter(24), TimeSpan.FromMinutes(3), GasMixture.Air, SegmentKind.Stop);
        var ascent = new DiveSegment(Depth.FromMeter(20), TimeSpan.FromMinutes(1), GasMixture.Air, SegmentKind.Ascent);
        var algorithm = new FakeAlgorithm(Depth.FromMeter(24), interLevelAscent: [stop, ascent]);
        var planner = new DivePlanner(algorithm);
        var request = CreateRequest([
            TestFactory.CreateSegment(30, 10),
            TestFactory.CreateSegment(20, 5)
        ]);

        // Act
        var plan = planner.CreatePlan(request);

        // Assert
        Assert.Equal(20, Assert.Single(algorithm.InterLevelTargetsMeter), Precision);
        Assert.Equal(SegmentKind.Stop, plan.ExpandedSegments[2].Kind);
        Assert.Equal(24, plan.ExpandedSegments[2].Depth.InMeter, Precision);
        Assert.Equal(SegmentKind.Ascent, plan.ExpandedSegments[3].Kind);
        Assert.Equal(SegmentKind.Bottom, plan.ExpandedSegments[4].Kind);
    }

    [Fact]
    public void CreatePlan_ShouldTravelTheInterLevelAscentWithoutTheModel_WhenTheCeilingIsShallowerThanTheTarget()
    {
        // Arrange
        var algorithm = new FakeAlgorithm(Depth.FromMeter(10));
        var planner = new DivePlanner(algorithm);
        var request = CreateRequest([
            TestFactory.CreateSegment(30, 10),
            TestFactory.CreateSegment(20, 5)
        ]);

        // Act
        var plan = planner.CreatePlan(request);

        // Assert
        Assert.Empty(algorithm.InterLevelTargetsMeter);
        Assert.Equal(SegmentKind.Ascent, plan.ExpandedSegments[2].Kind);
        Assert.Equal(20, plan.ExpandedSegments[2].Depth.InMeter, Precision);
    }

    [Fact]
    public void CreatePlan_ShouldTravelTheInterLevelAscentWithoutTheModel_WhenTheCeilingEqualsTheTargetDepth()
    {
        // Arrange
        var algorithm = new FakeAlgorithm(Depth.FromMeter(20));
        var planner = new DivePlanner(algorithm);
        var request = CreateRequest([
            TestFactory.CreateSegment(30, 10),
            TestFactory.CreateSegment(20, 5)
        ]);

        // Act
        var plan = planner.CreatePlan(request);

        // Assert
        Assert.Empty(algorithm.InterLevelTargetsMeter);
        Assert.Equal(SegmentKind.Ascent, plan.ExpandedSegments[2].Kind);
        Assert.Equal(20, plan.ExpandedSegments[2].Depth.InMeter, Precision);
    }

    [Fact]
    public void CreatePlan_ShouldNotConsultTheCeiling_WhenTheProfileOnlyDescends()
    {
        // Arrange
        var algorithm = new FakeAlgorithm(Depth.FromMeter(24));
        var planner = new DivePlanner(algorithm);
        var request = CreateRequest([
            TestFactory.CreateSegment(20, 5),
            TestFactory.CreateSegment(30, 10)
        ]);

        // Act
        var plan = planner.CreatePlan(request);

        // Assert
        Assert.Empty(algorithm.InterLevelTargetsMeter);
        Assert.Equal(SegmentKind.Descent, plan.ExpandedSegments[0].Kind);
    }

    [Fact]
    public void CreatePlan_ShouldAppendTheModelFinalAscent_WhenTheWorkingPhaseIsComplete()
    {
        // Arrange
        var finalAscent = new[]
        {
            TestFactory.CreateSegment(6, 3, kind: SegmentKind.Stop),
            TestFactory.CreateSegment(0, 6, kind: SegmentKind.Ascent)
        };
        var planner = new DivePlanner(new FakeAlgorithm(finalAscent: finalAscent));
        var request = CreateRequest([TestFactory.CreateSegment(30, 10)]);

        // Act
        var plan = planner.CreatePlan(request);

        // Assert
        Assert.Equal(4, plan.ExpandedSegments.Count);
        Assert.Equal(SegmentKind.Stop, plan.ExpandedSegments[2].Kind);
        Assert.Equal(SegmentKind.Ascent, plan.ExpandedSegments[3].Kind);
    }

    [Fact]
    public void CreatePlan_ShouldPassTheRequestToTheModel_WhenTheFinalAscentIsComputed()
    {
        // Arrange
        var algorithm = new FakeAlgorithm();
        var planner = new DivePlanner(algorithm);
        var request = CreateRequest([TestFactory.CreateSegment(30, 10)]);

        // Act
        planner.CreatePlan(request);

        // Assert
        Assert.Same(request, algorithm.FinalAscentRequest);
    }

    [Fact]
    public void CreatePlan_ShouldReturnTheSumOfEverySegmentDuration_WhenTheRuntimeIsComputed()
    {
        // Arrange
        var finalAscent = new[] { TestFactory.CreateSegment(0, 3, kind: SegmentKind.Ascent) };
        var planner = new DivePlanner(new FakeAlgorithm(finalAscent: finalAscent));
        var request = CreateRequest([TestFactory.CreateSegment(30, 10)]);

        // Act
        var plan = planner.CreatePlan(request);

        // Assert
        Assert.Equal(14.5, plan.TotalRuntime.TotalMinutes, Precision);
    }

    [Fact]
    public void CreatePlan_ShouldSelectTheRichestPermissibleGas_WhenSeveralCylindersAreCarried()
    {
        // Arrange
        var cylinders = new[]
        {
            TestFactory.CreateCylinder(GasMixture.Air),
            TestFactory.CreateCylinder(Nitrox50, 11, 200, CylinderPurpose.DecoGas)
        };
        var planner = new DivePlanner(new FakeAlgorithm());
        var request = CreateRequest([TestFactory.CreateSegment(10, 10)], cylinders);

        // Act
        var plan = planner.CreatePlan(request);

        // Assert
        Assert.Equal(Nitrox50, plan.ExpandedSegments[1].Gas);
    }

    [Fact]
    public void CreatePlan_ShouldExcludeGasBeyondTheBottomLimit_WhenTheLevelIsDeep()
    {
        // Arrange
        var cylinders = new[]
        {
            TestFactory.CreateCylinder(GasMixture.Air),
            TestFactory.CreateCylinder(Nitrox50, 11, 200, CylinderPurpose.DecoGas)
        };
        var planner = new DivePlanner(new FakeAlgorithm());
        var request = CreateRequest([TestFactory.CreateSegment(30, 10)], cylinders);

        // Act
        var plan = planner.CreatePlan(request);

        // Assert
        Assert.Equal(GasMixture.Air, plan.ExpandedSegments[1].Gas);
    }

    [Fact]
    public void CreatePlan_ShouldReportGasUsagePerCylinder_WhenTheSharedCalculatorsAreRun()
    {
        // Arrange
        var cylinders = new[]
        {
            TestFactory.CreateCylinder(GasMixture.Air),
            TestFactory.CreateCylinder(Nitrox50, 11, 200, CylinderPurpose.DecoGas)
        };
        var planner = new DivePlanner(new FakeAlgorithm());
        var request = CreateRequest([TestFactory.CreateSegment(30, 10)], cylinders);

        // Act
        var plan = planner.CreatePlan(request);

        // Assert
        Assert.Equal(2, plan.GasUsage.Count);
        Assert.True(plan.GasUsage[0].GasUsed.InLiter > 0);
        Assert.Equal(0, plan.GasUsage[1].GasUsed.InLiter, Precision);
    }

    [Fact]
    public void CreatePlan_ShouldReportTheReserveAssessment_WhenTheSharedCalculatorsAreRun()
    {
        // Arrange
        var planner = new DivePlanner(new FakeAlgorithm());
        var request = CreateRequest([TestFactory.CreateSegment(30, 10)]);

        // Act
        var plan = planner.CreatePlan(request);

        // Assert
        Assert.Single(plan.ReserveGas.CylinderStatuses);
    }

    [Fact]
    public void CreatePlan_ShouldReportTheOxygenExposure_WhenTheSharedCalculatorsAreRun()
    {
        // Arrange
        var planner = new DivePlanner(new FakeAlgorithm());
        var request = CreateRequest([TestFactory.CreateSegment(30, 10)]);

        // Act
        var plan = planner.CreatePlan(request);

        // Assert
        Assert.True(plan.OxygenToleranceUnits > 0);
        Assert.True(plan.CentralNervousSystemFraction > 0);
    }

    [Fact]
    public void CreatePlan_ShouldCarryTheOxygenExposureOfAPriorDive_WhenTheDiveIsRepetitive()
    {
        // Arrange
        var profile = new DiveProfile([TestFactory.CreateSegment(30, 10)]);
        Cylinder[] cylinders = [TestFactory.CreateCylinder(GasMixture.Air)];
        var settings = TestFactory.CreateSettings();
        var prior = new PriorDive(profile, cylinders, settings, TimeSpan.FromMinutes(90), GasMixture.Air);
        var single = new DivePlanner(new FakeAlgorithm()).CreatePlan(new DivePlanRequest(profile, cylinders, settings));

        // Act
        var repetitive = new DivePlanner(new FakeAlgorithm())
            .CreatePlan(new DivePlanRequest(profile, cylinders, settings, [prior]));

        // Assert
        Assert.Equal(single.CentralNervousSystemFraction * (0.5 + 1), repetitive.CentralNervousSystemFraction, Precision);
        Assert.Equal(single.OxygenToleranceUnits * 2, repetitive.OxygenToleranceUnits, Precision);
    }

    [Fact]
    public void CreatePlan_ShouldDecayEachPriorDivesExposureOverEveryLaterSurfaceInterval_WhenSeveralDivesPrecede()
    {
        // Arrange
        var profile = new DiveProfile([TestFactory.CreateSegment(30, 10)]);
        Cylinder[] cylinders = [TestFactory.CreateCylinder(GasMixture.Air)];
        var settings = TestFactory.CreateSettings();
        var prior = new PriorDive(profile, cylinders, settings, TimeSpan.FromMinutes(90), GasMixture.Air);
        var single = new DivePlanner(new FakeAlgorithm()).CreatePlan(new DivePlanRequest(profile, cylinders, settings));

        // Act
        var third = new DivePlanner(new FakeAlgorithm())
            .CreatePlan(new DivePlanRequest(profile, cylinders, settings, [prior, prior]));

        // Assert
        Assert.Equal(single.CentralNervousSystemFraction * (0.25 + 0.5 + 1), third.CentralNervousSystemFraction, Precision);
        Assert.Equal(single.OxygenToleranceUnits * 3, third.OxygenToleranceUnits, Precision);
    }

    [Fact]
    public void CreatePlan_ShouldPlanEveryPriorDiveOnTheDivesBeforeIt_WhenTheDiveIsRepetitive()
    {
        // Arrange
        var algorithm = new FakeAlgorithm();
        var profile = new DiveProfile([TestFactory.CreateSegment(30, 10)]);
        Cylinder[] cylinders = [TestFactory.CreateCylinder(GasMixture.Air)];
        var settings = TestFactory.CreateSettings();
        var prior = new PriorDive(profile, cylinders, settings, TimeSpan.FromMinutes(60), GasMixture.Air);

        // Act
        new DivePlanner(algorithm).CreatePlan(new DivePlanRequest(profile, cylinders, settings, [prior, prior]));

        // Assert
        Assert.Equal([0, 1, 2], algorithm.BeginDivePriorDiveCounts);
    }

    private sealed class FakeState : IDecompressionState
    {
    }

    private sealed class FakeAlgorithm : IDecompressionAlgorithm
    {
        private readonly Depth _ceiling;
        private readonly DiveSegment[] _finalAscent;
        private readonly DiveSegment[] _interLevelAscent;

        public FakeAlgorithm(Depth? ceiling = null,
            IEnumerable<DiveSegment>? finalAscent = null,
            IEnumerable<DiveSegment>? interLevelAscent = null)
        {
            _ceiling = ceiling ?? Depth.Zero;
            _finalAscent = finalAscent?.ToArray() ?? [];
            _interLevelAscent = interLevelAscent?.ToArray() ?? [];
        }

        public List<DiveSegment> LoadedSegments { get; } = [];

        public List<double> InterLevelTargetsMeter { get; } = [];

        public int BeginDiveCallCount => BeginDivePriorDiveCounts.Count;

        public List<int> BeginDivePriorDiveCounts { get; } = [];

        public DivePlanRequest? FinalAscentRequest { get; private set; }

        public IDecompressionState BeginDive(DivePlanRequest request)
        {
            BeginDivePriorDiveCounts.Add(request.PriorDives.Count);
            return new FakeState();
        }

        public IDecompressionState LoadSegment(IDecompressionState state, DiveSegment segment)
        {
            LoadedSegments.Add(segment);
            return state;
        }

        public Depth CurrentCeiling(IDecompressionState state) => _ceiling;

        public IReadOnlyList<DiveSegment> CalculateFinalAscent(IDecompressionState state, DivePlanRequest request)
        {
            FinalAscentRequest = request;
            return _finalAscent;
        }

        public IReadOnlyList<DiveSegment> CalculateAscent(IDecompressionState state,
            DivePlanRequest request,
            Depth targetDepth,
            BreathingLoop breathingLoop)
        {
            InterLevelTargetsMeter.Add(targetDepth.InMeter);
            return _interLevelAscent;
        }
    }
}