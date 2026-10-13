using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Export.Unit.Tests;

public sealed class ProfileChartTests
{
    private const int Precision = 9;

    private static readonly ChartArea Area = new(10, 20, 300, 150);

    [Fact]
    public void Create_ShouldThrowArgumentNullException_WhenTheResultIsNull()
    {
        // Arrange
        DiveResult result = null!;

        // Act
        Action act = () => ProfileChart.Create(result, UnitSystem.Metric);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Create_ShouldThrowArgumentException_WhenTheDiveCouldNotBePlanned()
    {
        // Arrange
        var result = TestFactory.Calculate(TestFactory.FailingPlan());

        // Act
        Action act = () => ProfileChart.Create(result, UnitSystem.Metric);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void Create_ShouldPlotTheCeilingAtEachTracedRuntime_WhenTheDiveHasDecompression()
    {
        // Arrange
        var result = TestFactory.Calculate(TestFactory.DecompressionPlan());

        // Act
        var chart = ProfileChart.Create(result, UnitSystem.Metric);

        // Assert
        Assert.Equal(
            result.Ceiling.Select(static point => new ChartPoint(point.Runtime.TotalMinutes, point.Depth)),
            chart.Ceiling);
        Assert.Contains(chart.Ceiling, static point => point.Depth > 0);
    }

    [Fact]
    public void Create_ShouldPlotNoCeiling_WhenTheDiveNeedsNoStops()
    {
        // Arrange
        var result = TestFactory.Calculate(TestFactory.NoStopPlan());

        // Act
        var chart = ProfileChart.Create(result, UnitSystem.Metric);

        // Assert
        Assert.Empty(chart.Ceiling);
    }

    [Fact]
    public void Create_ShouldStartAtTheSurfaceAndEndEachPhaseAtItsRuntimeAndDepth_WhenTheDiveWasPlanned()
    {
        // Arrange
        var result = TestFactory.Calculate(TestFactory.DecompressionPlan());

        // Act
        var chart = ProfileChart.Create(result, UnitSystem.Metric);

        // Assert
        Assert.Equal(new ChartPoint(0, 0), chart.Points[0]);
        Assert.Equal(
            result.Profile.Select(static phase => new ChartPoint(phase.Runtime.TotalMinutes, phase.EndDepth)),
            chart.Points.Skip(1));
        Assert.Equal(0, chart.Points[^1].Depth, Precision);
    }

    [Fact]
    public void Create_ShouldMarkEveryStopAndGasSwitchBetweenItsStartAndEndRuntime_WhenTheDiveHasDecompression()
    {
        // Arrange
        var result = TestFactory.Calculate(TestFactory.DecompressionPlan());
        var expected = new List<ChartStop>();
        var start = 0.0;
        foreach (var phase in result.Profile)
        {
            if (phase.Kind is SegmentKind.Stop or SegmentKind.GasSwitch)
            {
                expected.Add(new ChartStop(start, phase.Runtime.TotalMinutes, phase.EndDepth, phase.Gas,
                    phase.Kind == SegmentKind.GasSwitch));
            }

            start = phase.Runtime.TotalMinutes;
        }

        // Act
        var chart = ProfileChart.Create(result, UnitSystem.Metric);

        // Assert
        Assert.Equal(expected, chart.Stops);
        Assert.Contains(chart.Stops, static stop => stop is { IsGasSwitch: true, Gas: "NX50" });
    }

    [Fact]
    public void Create_ShouldMarkTheSafetyStop_WhenTheDiveMakesOne()
    {
        // Arrange
        var result = TestFactory.Calculate(TestFactory.SafetyStopPlan());

        // Act
        var chart = ProfileChart.Create(result, UnitSystem.Metric);

        // Assert
        var stop = Assert.Single(chart.Stops);
        Assert.True(stop.IsSafetyStop);
        Assert.False(stop.IsGasSwitch);
        Assert.Equal(6, stop.Depth, Precision);
        Assert.Equal(3, stop.EndMinutes - stop.StartMinutes, Precision);
    }

    [Fact]
    public void Create_ShouldLabelTheDepthEveryFiveMetersDownToThirty_WhenTheDiveReachesThirtyMeters()
    {
        // Arrange
        var result = TestFactory.Calculate(TestFactory.Plan());

        // Act
        var chart = ProfileChart.Create(result, UnitSystem.Metric);

        // Assert
        Assert.Equal([0.0, 5, 10, 15, 20, 25, 30], chart.DepthTicks);
        Assert.Equal(30, chart.MaximumDepth, Precision);
    }

    [Fact]
    public void Create_ShouldLabelTheDepthEveryTwentyFeetDownToOneHundred_WhenAnImperialDiveReachesOneHundredFeet()
    {
        // Arrange
        var result = TestFactory.Calculate(TestFactory.Plan(UnitSystem.Imperial));

        // Act
        var chart = ProfileChart.Create(result, UnitSystem.Imperial);

        // Assert
        Assert.Equal([0.0, 20, 40, 60, 80, 100], chart.DepthTicks);
    }

    [Fact]
    public void Create_ShouldLabelTimeInEqualStepsFromZeroToTheRuntimeRoundedUp_WhenTheDiveWasPlanned()
    {
        // Arrange
        var result = TestFactory.Calculate(TestFactory.DecompressionPlan());
        var runtime = result.TotalRuntime.TotalMinutes;

        // Act
        var chart = ProfileChart.Create(result, UnitSystem.Metric);

        // Assert
        var step = chart.TimeTicks[1];
        Assert.Equal(0, chart.TimeTicks[0], Precision);
        Assert.Equal(chart.TimeTicks.Select((_, index) => index * step), chart.TimeTicks);
        Assert.True(chart.MaximumMinutes >= runtime && chart.MaximumMinutes - step < runtime);
        Assert.True(chart.TimeTicks.Count - 1 <= 6);
    }

    [Fact]
    public void X_ShouldSpanTheAreaFromTheLeftEdgeAtZeroToTheRightEdgeAtTheMaximumRuntime_WhenMappingRuntimes()
    {
        // Arrange
        var chart = ProfileChart.Create(TestFactory.Calculate(TestFactory.Plan()), UnitSystem.Metric);

        // Act
        var left = chart.X(0, Area);
        var right = chart.X(chart.MaximumMinutes, Area);
        var middle = chart.X(chart.MaximumMinutes / 2, Area);

        // Assert
        Assert.Equal(10, left, Precision);
        Assert.Equal(310, right, Precision);
        Assert.Equal(160, middle, Precision);
    }

    [Fact]
    public void Y_ShouldPutTheSurfaceAtTheTopAndTheMaximumDepthAtTheBottom_WhenMappingDepths()
    {
        // Arrange
        var chart = ProfileChart.Create(TestFactory.Calculate(TestFactory.Plan()), UnitSystem.Metric);

        // Act
        var surface = chart.Y(0, Area);
        var bottom = chart.Y(chart.MaximumDepth, Area);

        // Assert
        Assert.Equal(20, surface, Precision);
        Assert.Equal(170, bottom, Precision);
    }
}
