using System.Text.RegularExpressions;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Export.Unit.Tests;

public sealed class DiveSheetTests
{
    private static string Sheet(DivePlanDocument plan) => TestFactory.Read(DiveSheet.Create(plan, TestFactory.Now));

    [Fact]
    public void Create_ShouldThrowArgumentNullException_WhenThePlanIsNull()
    {
        // Arrange
        DivePlanDocument plan = null!;

        // Act
        Action act = () => DiveSheet.Create(plan, TestFactory.Now);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Create_ShouldShowTheResultsChartProfileAndGasOfTheDive_WhenThePlanIsCalculated()
    {
        // Arrange
        var plan = TestFactory.DecompressionPlan();
        var result = TestFactory.Calculate(plan);

        // Act
        var pdf = Sheet(plan);

        // Assert
        Assert.Contains("/Title (Reef)", pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("Dive"), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown(Formats.Minutes(result.TotalRuntime)), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("NX50"), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("Depth (m)"), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("Used (L)"), pdf, StringComparison.Ordinal);
        Assert.Contains(" h f", pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown(DiveSheet.Disclaimer), pdf, StringComparison.Ordinal);
        Assert.DoesNotContain(TestFactory.Shown("Apparatus"), pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ShouldStartEachLaterDiveWithItsSurfaceInterval_WhenThePlanIsASeries()
    {
        // Arrange
        var plan = TestFactory.SeriesPlan();

        // Act
        var pdf = Sheet(plan);

        // Assert
        Assert.Contains(TestFactory.Shown("Dive 1"), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("Dive 2 - after 90 min on the surface"), pdf, StringComparison.Ordinal);
        Assert.Contains("/Count 2", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ShouldListTheIssues_WhenThePlanCannotBeCalculated()
    {
        // Arrange
        var plan = TestFactory.InvalidPlan() with { Name = " " };

        // Act
        var pdf = Sheet(plan);

        // Assert
        Assert.Contains("/Title (Dive plan)", pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("This plan cannot be calculated:"), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("- Dive 1 · Level 1: The depth must be greater than 0."), pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ShouldShowWhyTheDiveFailed_WhenTheDiveCannotBePlanned()
    {
        // Arrange
        var plan = TestFactory.FailingPlan();
        var result = TestFactory.Calculate(plan);

        // Act
        var pdf = Sheet(plan);

        // Assert
        Assert.Contains(TestFactory.Shown(result.FailureMessage!), pdf, StringComparison.Ordinal);
        Assert.DoesNotContain(TestFactory.Shown("Runtime"), pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ShouldDrawTheDiveAndSayHowMuchGasIsMissing_WhenACylinderRunsOut()
    {
        // Arrange
        var plan = TestFactory.OutOfGasPlan();
        var cylinder = TestFactory.Calculate(plan).Cylinders[0];

        // Act
        var pdf = Sheet(plan);

        // Assert
        Assert.Contains(TestFactory.Shown("Runtime"), pdf, StringComparison.Ordinal);
        Assert.Contains(
            TestFactory.Shown($"Air runs out: the dive needs {Formats.Number(cylinder.Shortfall)} L more than the cylinder holds."),
            pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ShouldWarnAboutTheCeilingAndTheReserve_WhenTheDiveBreaksThem()
    {
        // Arrange
        var plan = TestFactory.ViolatingPlan();

        // Act
        var pdf = Sheet(plan);

        // Assert
        Assert.Matches(@"\(Ascent from 40 to 3 m passes the decompression ceiling at [\d.]+ m\.\) Tj", pdf);
        Assert.Contains(TestFactory.Shown("A cylinder does not hold the reserve gas the plan requires."), pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ShouldShowTheApparatusColumnInImperialUnits_WhenTheDiveIsOnARebreather()
    {
        // Arrange
        var plan = TestFactory.ClosedCircuitPlan();

        // Act
        var pdf = Sheet(plan);

        // Assert
        Assert.Contains(TestFactory.Shown("Apparatus"), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("Depth (ft)"), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("Pressure (psi)"), pdf, StringComparison.Ordinal);
        Assert.Matches(Regex.Escape("(CCR @ 1.3) Tj"), pdf);
    }
}
