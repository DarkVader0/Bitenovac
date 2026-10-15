using Bitenovac.DecompressionAlgorithms.Core.Equipment;
using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Export.Unit.Tests;

public sealed class PlanSummaryTests
{
    private sealed record UnknownAlgorithmSettings : AlgorithmSettings;

    [Fact]
    public void Text_ShouldThrowArgumentNullException_WhenThePlanIsNull()
    {
        // Arrange
        DivePlanDocument plan = null!;

        // Act
        Action act = () => PlanSummary.Text(plan, TestFactory.Now);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Text_ShouldDescribeThePlanItsSettingsAndTheDive_WhenThePlanIsCalculated()
    {
        // Arrange
        var plan = TestFactory.DecompressionPlan();
        var result = TestFactory.Calculate(plan);
        var stops = string.Join(", ", result.Profile
            .Where(static phase => phase.Kind == SegmentKind.Stop)
            .Select(static phase => $"{Formats.Number(phase.EndDepth)} m {Formats.Clock(phase.Duration)} min"));

        // Act
        var lines = PlanSummary.Text(plan, TestFactory.Now).Split(Environment.NewLine);

        // Assert
        Assert.Equal("Reef", lines[0]);
        Assert.Equal("Bühlmann ZHL-16C GF 30/70 · Metric · exported 11 Oct 2026 12:00 UTC", lines[1]);
        Assert.Equal("Salt water · altitude 0 m · SAC 18 / 14 L/min · PO2 1.4 / 1.6 bar · last stop 3 m · safety stop off", lines[2]);
        Assert.Equal("Dive: Open circuit · 40 m 25 min · Air 24.0 L at 230 bar (Bottom gas), NX50 11.0 L at 200 bar (Deco gas)", lines[4]);
        Assert.Equal($"Stops: {stops}", lines[5]);
        Assert.StartsWith($"Runtime {Formats.Minutes(result.TotalRuntime)} · max 40 m · CNS ", lines[6], StringComparison.Ordinal);
        Assert.StartsWith("Air: ", lines[7], StringComparison.Ordinal);
        Assert.Equal(DiveSheet.Disclaimer, lines[^1]);
    }

    [Fact]
    public void Text_ShouldListEachContingencyWithItsRuntimeStopsAndCns_WhenTheDiveCarriesADecoGas()
    {
        // Arrange
        var plan = TestFactory.DecompressionPlan();
        var contingencies = TestFactory.Calculate(plan).Contingencies;
        var extended = contingencies[0].Result;
        var lost = contingencies[1].Result;

        // Act
        var lines = PlanSummary.Text(plan, TestFactory.Now).Split(Environment.NewLine);

        // Assert
        var start = Array.IndexOf(lines, "Contingencies:");
        Assert.True(start > 0);
        Assert.StartsWith($"- +3 min, +3 m: runtime {Formats.Minutes(extended.TotalRuntime)} · stops ", lines[start + 1], StringComparison.Ordinal);
        Assert.StartsWith($"- NX50 lost: runtime {Formats.Minutes(lost.TotalRuntime)} · stops ", lines[start + 2], StringComparison.Ordinal);
        Assert.EndsWith($" · CNS {Formats.Number(lost.CnsPercent)} %", lines[start + 2], StringComparison.Ordinal);
    }

    [Fact]
    public void Text_ShouldSayNoDecompressionStops_WhenAContingencyNeedsNone()
    {
        // Arrange
        var plan = TestFactory.NoStopPlan();

        // Act
        var text = PlanSummary.Text(plan, TestFactory.Now);

        // Assert
        Assert.Contains($"{Environment.NewLine}- +3 min, +3 m: runtime ", text, StringComparison.Ordinal);
        Assert.Contains("no decompression stops", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_ShouldSayAContingencyCannotBePlanned_WhenItsGasIsLost()
    {
        // Arrange
        var plan = TestFactory.Plan() with
        {
            Dives = [new DiveInput(0, new OpenCircuitInput(), [new LevelInput(12, 10)], [new CylinderInput(50, 0, 11, 0, 200, CylinderPurpose.DecoGas)])]
        };

        // Act
        var text = PlanSummary.Text(plan, TestFactory.Now);

        // Assert
        Assert.Contains("- NX50 lost: cannot be planned: No gas is left to breathe.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_ShouldSayThereAreNoStopsAndTheReserveIsNotMet_WhenAShortDiveRunsLowOnGas()
    {
        // Arrange
        var plan = TestFactory.Plan() with
        {
            Dives = [TestFactory.Plan().Dives[0] with { Levels = [new LevelInput(10, 10)], Cylinders = [new(21, 0, 3, 0, 200, CylinderPurpose.BottomGas)] }]
        };

        // Act
        var text = PlanSummary.Text(plan, TestFactory.Now);

        // Assert
        Assert.Contains($"{Environment.NewLine}No stops{Environment.NewLine}", text, StringComparison.Ordinal);
        Assert.Contains(" - reserve not met", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_ShouldNameTheSafetyStopApartFromDecompressionStops_WhenTheDiveNeedsNone()
    {
        // Arrange
        var plan = TestFactory.SafetyStopPlan();

        // Act
        var text = PlanSummary.Text(plan, TestFactory.Now);

        // Assert
        Assert.Contains($"{Environment.NewLine}No decompression stops · safety stop 6 m 3 min{Environment.NewLine}", text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Text_ShouldSayHowMuchGasIsMissing_WhenACylinderRunsOut()
    {
        // Arrange
        var plan = TestFactory.OutOfGasPlan();
        var cylinder = TestFactory.Calculate(plan).Cylinders[0];

        // Act
        var text = PlanSummary.Text(plan, TestFactory.Now);

        // Assert
        Assert.Contains($" - runs out, {Formats.Number(cylinder.Shortfall)} L short", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_ShouldNameEachDiveWithItsSurfaceInterval_WhenThePlanIsASeries()
    {
        // Arrange
        var plan = TestFactory.SeriesPlan();

        // Act
        var text = PlanSummary.Text(plan, TestFactory.Now);

        // Assert
        Assert.Contains($"{Environment.NewLine}Dive 1: Open circuit", text, StringComparison.Ordinal);
        Assert.Contains($"{Environment.NewLine}Dive 2 (after 1:30 h on the surface): Open circuit", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_ShouldListTheSettingsOfADiveThatHasItsOwn_WhenThePlanIsASeries()
    {
        // Arrange
        var series = TestFactory.SeriesPlan();
        var own = series.Settings with { BottomSac = 25, Altitude = 300 };
        var plan = series with { Dives = [series.Dives[0], series.Dives[1] with { Settings = own }] };

        // Act
        var text = PlanSummary.Text(plan, TestFactory.Now);

        // Assert
        Assert.Contains("Settings: Salt water · altitude 300 m · SAC 25 / 14 L/min · PO2 1.4 / 1.6 bar · last stop 3 m · safety stop off", text, StringComparison.Ordinal);
        Assert.Equal(1, text.Split("Settings: ").Length - 1);
    }

    [Fact]
    public void Text_ShouldNameTheDiveByItsOwnName_WhenTheDiveIsNamed()
    {
        // Arrange
        var series = TestFactory.SeriesPlan();
        var plan = series with { Dives = [series.Dives[0], series.Dives[1] with { Name = "Wreck" }] };
        var single = TestFactory.Plan() with { Dives = [TestFactory.Plan().Dives[0] with { Name = "Reef" }] };

        // Act
        var seriesText = PlanSummary.Text(plan, TestFactory.Now);
        var singleText = PlanSummary.Text(single, TestFactory.Now);

        // Assert
        Assert.Contains($"{Environment.NewLine}Wreck (after 1:30 h on the surface): Open circuit", seriesText, StringComparison.Ordinal);
        Assert.Contains($"{Environment.NewLine}Reef: Open circuit", singleText, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_ShouldSayTheDiveCannotBePlanned_WhenThePlannerFails()
    {
        // Arrange
        var plan = TestFactory.FailingPlan();
        var result = TestFactory.Calculate(plan);

        // Act
        var text = PlanSummary.Text(plan, TestFactory.Now);

        // Assert
        Assert.Contains($"Cannot be planned: {result.FailureMessage}", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_ShouldWarnAboutTheCeiling_WhenAnAscentBetweenLevelsPassesIt()
    {
        // Arrange
        var plan = TestFactory.ViolatingPlan();

        // Act
        var text = PlanSummary.Text(plan, TestFactory.Now);

        // Assert
        Assert.Contains("Warning: an ascent between levels passes the decompression ceiling.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_ShouldListTheIssuesAndAnUntitledName_WhenThePlanCannotBeCalculated()
    {
        // Arrange
        var plan = TestFactory.InvalidPlan() with { Name = "", Algorithm = new UnknownAlgorithmSettings() };

        // Act
        var lines = PlanSummary.Text(plan, TestFactory.Now).Split(Environment.NewLine);

        // Assert
        Assert.Equal("Dive plan", lines[0]);
        Assert.StartsWith("Unknown algorithm · ", lines[1], StringComparison.Ordinal);
        Assert.Contains("This plan cannot be calculated:", lines);
        Assert.Contains("- Dive 1 · Level 1: The depth must be greater than 0.", lines);
        Assert.Contains("- Algorithm: The algorithm is not one this app knows.", lines);
    }

    [Fact]
    public void Text_ShouldUseImperialUnitsAndTheSixMeterLastStop_WhenTheSettingsSaySo()
    {
        // Arrange
        var imperial = TestFactory.Plan(UnitSystem.Imperial);
        var plan = imperial with { Settings = imperial.Settings with { LastStopAtSixMeters = true, SafetyStop = true } };
        var metric = TestFactory.Plan() with { Settings = TestFactory.Plan().Settings with { LastStopAtSixMeters = true } };

        // Act
        var imperialText = PlanSummary.Text(plan, TestFactory.Now);
        var metricText = PlanSummary.Text(metric, TestFactory.Now);

        // Assert
        Assert.Contains("Imperial", imperialText, StringComparison.Ordinal);
        Assert.Contains("SAC 0.65 / 0.50 cu ft/min", imperialText, StringComparison.Ordinal);
        Assert.Contains("last stop 20 ft · safety stop on", imperialText, StringComparison.Ordinal);
        Assert.Contains("last stop 6 m", metricText, StringComparison.Ordinal);
        Assert.Contains("last stop 10 ft", PlanSummary.Text(imperial, TestFactory.Now), StringComparison.Ordinal);
    }
}
