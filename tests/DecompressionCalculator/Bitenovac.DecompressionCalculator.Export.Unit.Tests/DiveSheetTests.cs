using Bitenovac.DecompressionAlgorithms.Core.Equipment;
using System.Text.RegularExpressions;
using Bitenovac.DecompressionCalculator.Export.Pdf;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Export.Unit.Tests;

public sealed class DiveSheetTests
{
    private const string AppVersion = "2026.10.13.001";
    private const string CeilingStroke = "0.702 0.129 0.129 RG 2.8 w";
    private const string CeilingZoneFill = "0.816 0.612 0.635 rg";
    private const string DecompressionStopStroke = "0.702 0.129 0.129 RG 3 w";
    private const string SafetyStopStroke = "0.91 0.537 0.047 RG 3 w";
    private const string ChartTimeLabel = "(min) Tj";

    private static readonly ResultBlock[] OnePageBlocks = [ResultBlock.Figures, ResultBlock.Profile, ResultBlock.Gas, ResultBlock.Chart];

    private static string Sheet(DivePlanDocument plan, int dive = 0) =>
        TestFactory.Read(DiveSheet.Create(plan, dive, TestFactory.Now, AppVersion, OnePageBlocks));

    [Fact]
    public void Create_ShouldThrowArgumentNullException_WhenThePlanIsNull()
    {
        // Arrange
        DivePlanDocument plan = null!;

        // Act
        Action act = () => DiveSheet.Create(plan, 0, TestFactory.Now, AppVersion);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Create_ShouldThrowArgumentNullException_WhenTheAppVersionIsNull()
    {
        // Arrange
        var plan = TestFactory.Plan();

        // Act
        Action act = () => DiveSheet.Create(plan, 0, TestFactory.Now, null!);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void Create_ShouldThrowArgumentOutOfRangeException_WhenTheDiveIsNotInThePlan(int dive)
    {
        // Arrange
        var plan = TestFactory.Plan();

        // Act
        Action act = () => DiveSheet.Create(plan, dive, TestFactory.Now, AppVersion);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void Create_ShouldPutTheDiveDateAndAppVersionBesideTheTitle_WhenTheyFitOneLine()
    {
        // Arrange
        var plan = TestFactory.DecompressionPlan();

        // Act
        var pdf = Sheet(plan);

        // Assert
        var meta = TestFactory.Shown($"Dive · 11 Oct 2026 12:00 UTC · Bitenovac {AppVersion}");
        Assert.Contains("/Title (Reef)", pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("Reef"), pdf, StringComparison.Ordinal);
        Assert.Contains(meta, pdf, StringComparison.Ordinal);
        Assert.DoesNotMatch($@"rg 32 [\d.]+ Td {Regex.Escape(meta)}", pdf);
    }

    [Fact]
    public void Create_ShouldPutTheDiveDateAndAppVersionUnderTheTitle_WhenTheTitleIsTooLongToShareALine()
    {
        // Arrange
        var plan = TestFactory.DecompressionPlan() with { Name = "Wreck of the steamship on the outer reef, second visit" };

        // Act
        var pdf = Sheet(plan);

        // Assert
        var meta = TestFactory.Shown($"Dive · 11 Oct 2026 12:00 UTC · Bitenovac {AppVersion}");
        Assert.Matches($@"rg 32 [\d.]+ Td {Regex.Escape(meta)}", pdf);
    }

    [Fact]
    public void Create_ShouldShowTheFiguresProfileGasChartAndWarningOnOnePage_WhenThePlanIsCalculated()
    {
        // Arrange
        var plan = TestFactory.DecompressionPlan();
        var result = TestFactory.Calculate(plan);

        // Act
        var pdf = Sheet(plan);

        // Assert
        Assert.Contains("/Count 1", pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown(Formats.Minutes(result.TotalRuntime)), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("TTS"), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("Runtime (min)"), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("Depth (m)"), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("Used (bar)"), pdf, StringComparison.Ordinal);
        Assert.Contains(ChartTimeLabel, pdf, StringComparison.Ordinal);
        Assert.DoesNotContain(TestFactory.Shown("Apparatus"), pdf, StringComparison.Ordinal);
        Assert.DoesNotContain(TestFactory.Shown("Missing (bar)"), pdf, StringComparison.Ordinal);
        Assert.All(SheetWriter.Wrap(DiveSheet.Disclaimer, PdfFont.HelveticaBold, 7.5, SheetWriter.ContentWidth),
            line => Assert.Contains(TestFactory.Shown(line), pdf, StringComparison.Ordinal));
    }

    [Fact]
    public void Create_ShouldPrintDepthAndRuntimeInBold_WhenTheDiveIsPlanned()
    {
        // Arrange
        var plan = TestFactory.DecompressionPlan();
        var result = TestFactory.Calculate(plan);

        // Act
        var pdf = Sheet(plan);

        // Assert
        var runtime = TestFactory.Shown(Formats.Clock(result.Profile[0].Runtime));
        Assert.Matches($@"/F2 [\d.]+ Tf [\d. ]+rg [\d.]+ [\d.]+ Td {Regex.Escape(runtime)}", pdf);
    }

    [Fact]
    public void Create_ShouldTitleTheDiveByItsNameAndPlace_WhenADiveOfASeriesIsNamed()
    {
        // Arrange
        var series = TestFactory.SeriesPlan();
        var plan = series with { Dives = [series.Dives[0], series.Dives[1] with { Name = " Wreck " }] };

        // Act
        var pdf = Sheet(plan, 1);

        // Assert
        Assert.Contains(TestFactory.Shown($"Wreck (2 of 2) · 11 Oct 2026 12:00 UTC · Bitenovac {AppVersion}"), pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ShouldShowTheSettingsTheDiveIsPlannedWith_WhenItHasItsOwn()
    {
        // Arrange
        var series = TestFactory.SeriesPlan();
        var own = series.Settings with { BottomSac = 25 };
        var plan = series with { Dives = [series.Dives[0], series.Dives[1] with { Settings = own }] };

        // Act
        var first = Sheet(plan);
        var second = Sheet(plan, 1);

        // Assert
        Assert.Contains("SAC 18 / 14 L/min", first, StringComparison.Ordinal);
        Assert.Contains("SAC 25 / 14 L/min", second, StringComparison.Ordinal);
        Assert.DoesNotContain("SAC 25", first, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ShouldTitleTheDiveByItsName_WhenTheOnlyDiveIsNamed()
    {
        // Arrange
        var single = TestFactory.DecompressionPlan();
        var plan = single with { Dives = [single.Dives[0] with { Name = "Wreck" }] };

        // Act
        var pdf = Sheet(plan);

        // Assert
        Assert.Contains(TestFactory.Shown($"Wreck · 11 Oct 2026 12:00 UTC · Bitenovac {AppVersion}"), pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ShouldShowOnlyTheChosenDiveAfterItsSurfaceInterval_WhenThePlanIsASeries()
    {
        // Arrange
        var plan = TestFactory.SeriesPlan();

        // Act
        var pdf = Sheet(plan, 1);

        // Assert
        Assert.Contains(TestFactory.Shown($"Dive 2 of 2 · 11 Oct 2026 12:00 UTC · Bitenovac {AppVersion}"), pdf, StringComparison.Ordinal);
        Assert.Matches(Regex.Escape("(After 1:30 h on the surface · Open circuit · 20 m 30 min"), pdf);
        Assert.DoesNotMatch(Regex.Escape("40 m 25 min"), pdf);
        Assert.Contains("/Count 1", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ShouldShrinkTheProfileAndMoveTheChartToTheNextPage_WhenTheProfileIsTooLongForBoth()
    {
        // Arrange
        var plan = TestFactory.LongPlan();

        // Act
        var pdf = Sheet(plan);

        // Assert
        Assert.Contains("/Count 2", pdf, StringComparison.Ordinal);
        Assert.Contains(ChartTimeLabel, pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("Missing (bar)"), pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ShouldShowTheBlocksInTheOrderGiven_WhenTheBlocksAreReordered()
    {
        // Arrange
        var plan = TestFactory.DecompressionPlan();

        // Act
        var pdf = TestFactory.Read(DiveSheet.Create(plan, 0, TestFactory.Now, AppVersion, [ResultBlock.Chart, ResultBlock.Gas, ResultBlock.Profile]));

        // Assert
        var chart = pdf.IndexOf(ChartTimeLabel, StringComparison.Ordinal);
        var gas = pdf.IndexOf(TestFactory.Shown("Used (bar)"), StringComparison.Ordinal);
        var profile = pdf.IndexOf(TestFactory.Shown("Depth (m)"), StringComparison.Ordinal);
        Assert.True(chart > 0 && chart < gas && gas < profile);
    }

    [Fact]
    public void Create_ShouldLeaveOutTheBlocksNotGiven_WhenSomeBlocksAreHidden()
    {
        // Arrange
        var plan = TestFactory.DecompressionPlan();

        // Act
        var pdf = TestFactory.Read(DiveSheet.Create(plan, 0, TestFactory.Now, AppVersion, [ResultBlock.Profile]));

        // Assert
        Assert.Contains(TestFactory.Shown("Depth (m)"), pdf, StringComparison.Ordinal);
        Assert.DoesNotContain(TestFactory.Shown("TTS"), pdf, StringComparison.Ordinal);
        Assert.DoesNotContain(TestFactory.Shown("Used (bar)"), pdf, StringComparison.Ordinal);
        Assert.DoesNotContain(ChartTimeLabel, pdf, StringComparison.Ordinal);
        Assert.DoesNotContain(TestFactory.Shown("Contingency"), pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ShouldShowOnlyTheTitleAndInputs_WhenNoBlockIsShown()
    {
        // Arrange
        var plan = TestFactory.DecompressionPlan();

        // Act
        var pdf = TestFactory.Read(DiveSheet.Create(plan, 0, TestFactory.Now, AppVersion, []));

        // Assert
        Assert.Contains("/Count 1", pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("Reef"), pdf, StringComparison.Ordinal);
        Assert.DoesNotContain(TestFactory.Shown("Depth (m)"), pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ShouldGiveEachContingencyItsOwnPageWithTheFullDive_WhenTheContingenciesBlockIsShown()
    {
        // Arrange
        var plan = TestFactory.DecompressionPlan();
        var contingencies = TestFactory.Calculate(plan).Contingencies;

        // Act
        var pdf = TestFactory.Read(DiveSheet.Create(plan, 0, TestFactory.Now, AppVersion, [ResultBlock.Profile, ResultBlock.Contingencies]));

        // Assert
        Assert.Equal(2, contingencies.Count);
        Assert.Contains($"/Count {1 + contingencies.Count}", pdf, StringComparison.Ordinal);
        Assert.Equal(1 + contingencies.Count, Regex.Count(pdf, Regex.Escape(TestFactory.Shown("Depth (m)"))));
        Assert.Contains(TestFactory.Shown("+3 min, +3 m"), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("NX50 lost"), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown(Formats.Clock(contingencies[1].Result.TotalRuntime)), pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ShouldShowEveryBlockAndEachContingencyPage_WhenNoBlocksAreGiven()
    {
        // Arrange
        var plan = TestFactory.DecompressionPlan();
        var contingencies = TestFactory.Calculate(plan).Contingencies;

        // Act
        var pdf = TestFactory.Read(DiveSheet.Create(plan, 0, TestFactory.Now, AppVersion));

        // Assert
        Assert.Contains($"/Count {1 + contingencies.Count}", pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("NX50 lost"), pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ShouldPrintNoContingencyAtAll_WhenTheContingenciesBlockIsHidden()
    {
        // Arrange
        var plan = TestFactory.DecompressionPlan();

        // Act
        var pdf = TestFactory.Read(DiveSheet.Create(plan, 0, TestFactory.Now, AppVersion, [ResultBlock.Profile, ResultBlock.Gas]));

        // Assert
        Assert.Contains("/Count 1", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain(TestFactory.Shown("NX50 lost"), pdf, StringComparison.Ordinal);
        Assert.DoesNotContain(TestFactory.Shown("+3 min, +3 m"), pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ShouldSayAContingencyCannotBePlanned_WhenItsGasIsLost()
    {
        // Arrange
        var plan = TestFactory.Plan() with
        {
            Dives = [new DiveInput(0, new OpenCircuitInput(), [new LevelInput(12, 10)], [new CylinderInput(50, 0, 11, 0, 200, CylinderPurpose.DecoGas)])]
        };

        // Act
        var pdf = TestFactory.Read(DiveSheet.Create(plan, 0, TestFactory.Now, AppVersion, [ResultBlock.Contingencies]));

        // Assert
        Assert.Contains(TestFactory.Shown("No gas is left to breathe."), pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ShouldKeepTheDisclaimerOnEveryPage_WhenTheSheetRunsToASecondPage()
    {
        // Arrange
        var plan = TestFactory.LongPlan();

        // Act
        var pdf = Sheet(plan);

        // Assert
        var lines = SheetWriter.Wrap(DiveSheet.Disclaimer, PdfFont.HelveticaBold, 7.5, SheetWriter.ContentWidth);
        Assert.All(lines, line => Assert.Equal(2, Regex.Count(pdf, Regex.Escape(TestFactory.Shown(line)))));
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
    public void Create_ShouldDrawTheCeilingAndFillTheZoneAboveIt_WhenTheDiveHasDecompression()
    {
        // Arrange
        var plan = TestFactory.DecompressionPlan();

        // Act
        var pdf = Sheet(plan);

        // Assert
        Assert.Contains(CeilingStroke, pdf, StringComparison.Ordinal);
        Assert.Contains(CeilingZoneFill, pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ShouldDrawDecompressionStopsInRed_WhenTheDiveHasThem()
    {
        // Arrange
        var plan = TestFactory.DecompressionPlan();

        // Act
        var pdf = Sheet(plan);

        // Assert
        Assert.Contains(DecompressionStopStroke, pdf, StringComparison.Ordinal);
        Assert.DoesNotContain(SafetyStopStroke, pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ShouldDrawTheSafetyStopInOrange_WhenTheDiveMakesOne()
    {
        // Arrange
        var plan = TestFactory.SafetyStopPlan();

        // Act
        var pdf = Sheet(plan);

        // Assert
        Assert.Contains(SafetyStopStroke, pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("Safety stop"), pdf, StringComparison.Ordinal);
        Assert.DoesNotContain(DecompressionStopStroke, pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ShouldDrawNoCeiling_WhenTheDiveNeedsNoStops()
    {
        // Arrange
        var plan = TestFactory.NoStopPlan();

        // Act
        var pdf = Sheet(plan);

        // Assert
        Assert.Contains(ChartTimeLabel, pdf, StringComparison.Ordinal);
        Assert.DoesNotContain(CeilingStroke, pdf, StringComparison.Ordinal);
        Assert.DoesNotContain(CeilingZoneFill, pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ShouldShowTheMissingGasInTheGasTableAndAWarning_WhenACylinderRunsOut()
    {
        // Arrange
        var plan = TestFactory.OutOfGasPlan();
        var cylinder = TestFactory.Calculate(plan).Cylinders[0];

        // Act
        var pdf = Sheet(plan);

        // Assert
        Assert.Contains(TestFactory.Shown("Missing (bar)"), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown(Formats.Number(GasAmounts.For(cylinder, GasQuantity.Pressure, UnitSystem.Metric).Start)), pdf, StringComparison.Ordinal);
        Assert.Contains(
            TestFactory.Shown($"Air runs out: the dive needs {Formats.Number(GasAmounts.For(cylinder, GasQuantity.Pressure, UnitSystem.Metric).Missing)} bar more than the cylinder holds."),
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
        Assert.Contains(TestFactory.Shown("Start (psi)"), pdf, StringComparison.Ordinal);
        Assert.Matches(Regex.Escape("(CCR @ 1.3) Tj"), pdf);
    }

    [Fact]
    public void Create_ShouldShowADifferentSheet_WhenAscentsAreMerged()
    {
        // Arrange
        var plan = TestFactory.DecompressionPlan();
        var merged = plan with { Settings = plan.Settings with { MergeAscentsIntoStops = true } };

        // Act
        var pdf = DiveSheet.Create(merged, 0, TestFactory.Now, AppVersion, OnePageBlocks);

        // Assert
        Assert.NotEqual(DiveSheet.Create(plan, 0, TestFactory.Now, AppVersion, OnePageBlocks), pdf);
    }

    [Fact]
    public void Create_ShouldShowTheSameSheet_WhenNoStopIsMerged()
    {
        // Arrange
        var plan = TestFactory.DecompressionPlan();
        var unmerged = plan with { Settings = plan.Settings with { MergeAscentsIntoStops = false, MergeDescentsIntoStops = false } };

        // Act
        var pdf = DiveSheet.Create(unmerged, 0, TestFactory.Now, AppVersion, OnePageBlocks);

        // Assert
        Assert.Equal(DiveSheet.Create(plan, 0, TestFactory.Now, AppVersion, OnePageBlocks), pdf);
    }

    [Fact]
    public void CreateSeries_ShouldShowADifferentSheet_WhenAscentsAreMerged()
    {
        // Arrange
        var plan = TestFactory.DecompressionPlan();
        var merged = plan with { Settings = plan.Settings with { MergeAscentsIntoStops = true } };

        // Act
        var pdf = DiveSheet.CreateSeries(merged, TestFactory.Now, AppVersion);

        // Assert
        Assert.NotEqual(DiveSheet.CreateSeries(plan, TestFactory.Now, AppVersion), pdf);
    }
}
