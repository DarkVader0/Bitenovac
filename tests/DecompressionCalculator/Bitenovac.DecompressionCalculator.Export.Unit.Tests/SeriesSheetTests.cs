using System.Text.RegularExpressions;
using Bitenovac.DecompressionAlgorithms.Core.Equipment;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Export.Unit.Tests;

public sealed class SeriesSheetTests
{
    private const string AppVersion = "2026.10.13.001";

    private static readonly ResultBlock[] OnePageBlocks = [ResultBlock.Figures, ResultBlock.Profile, ResultBlock.Gas, ResultBlock.Chart];

    private static string Series(DivePlanDocument plan, IReadOnlyList<ResultBlock>? blocks = null) =>
        TestFactory.Read(DiveSheet.CreateSeries(plan, TestFactory.Now, AppVersion, blocks ?? OnePageBlocks));

    [Fact]
    public void CreateSeries_ShouldThrowArgumentNullException_WhenThePlanIsNull()
    {
        // Arrange
        DivePlanDocument plan = null!;

        // Act
        Action act = () => DiveSheet.CreateSeries(plan, TestFactory.Now, AppVersion);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void CreateSeries_ShouldThrowArgumentNullException_WhenTheAppVersionIsNull()
    {
        // Arrange
        var plan = TestFactory.SeriesPlan();

        // Act
        Action act = () => DiveSheet.CreateSeries(plan, TestFactory.Now, null!);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void CreateSeries_ShouldMakeAnOverviewPageAndAPageForEachDive_WhenThePlanIsASeries()
    {
        // Arrange
        var plan = TestFactory.SeriesPlan();

        // Act
        var pdf = Series(plan);

        // Assert
        Assert.Contains("/Count 3", pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown($"2 dives · 11 Oct 2026 12:00 UTC · Bitenovac {AppVersion}"), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown($"Dive 1 of 2 · 11 Oct 2026 12:00 UTC · Bitenovac {AppVersion}"), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown($"Dive 2 of 2 · 11 Oct 2026 12:00 UTC · Bitenovac {AppVersion}"), pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateSeries_ShouldListEachDiveWithItsIntervalAndFigures_WhenThePlanIsASeries()
    {
        // Arrange
        var plan = TestFactory.SeriesPlan();
        var results = TestFactory.CalculateAll(plan);

        // Act
        var pdf = Series(plan);

        // Assert
        Assert.Contains(TestFactory.Shown("SI (h:mm)"), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("1:30"), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown(Formats.Clock(results[0].TotalRuntime)), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown(Formats.Clock(results[1].TotalRuntime)), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown(Formats.Number(results[1].CnsPercent)), pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateSeries_ShouldNameTheDivesByTheirNames_WhenTheyAreNamed()
    {
        // Arrange
        var series = TestFactory.SeriesPlan();
        var plan = series with { Dives = [series.Dives[0] with { Name = "Reef" }, series.Dives[1] with { Name = "Wreck" }] };

        // Act
        var pdf = Series(plan);

        // Assert
        Assert.Contains(TestFactory.Shown("Reef"), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("Wreck"), pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateSeries_ShouldShowTheBlocksGivenOnEveryDive_WhenTheyAreChosen()
    {
        // Arrange
        var plan = TestFactory.SeriesPlan();

        // Act
        var pdf = Series(plan, [ResultBlock.Gas]);

        // Assert
        Assert.Equal(2, Regex.Count(pdf, Regex.Escape(TestFactory.Shown("Used (L)"))));
        Assert.DoesNotContain(TestFactory.Shown("Time (min)"), pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateSeries_ShouldSayADiveCannotBePlanned_WhenOneOfTheDivesFails()
    {
        // Arrange
        var series = TestFactory.SeriesPlan();
        var plan = series with
        {
            Dives = [series.Dives[0], series.Dives[1] with { Cylinders = [new CylinderInput(100, 0, 7, 0, 200, CylinderPurpose.DecoGas)] }]
        };

        // Act
        var pdf = Series(plan);

        // Assert
        Assert.Contains(TestFactory.Shown("cannot be planned"), pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateSeries_ShouldListTheIssuesOnTheFirstPageAlone_WhenThePlanCannotBeCalculated()
    {
        // Arrange
        var series = TestFactory.SeriesPlan();
        var plan = series with { Dives = [series.Dives[0] with { Levels = [new LevelInput(-1, 20)] }, series.Dives[1]] };

        // Act
        var pdf = Series(plan);

        // Assert
        Assert.Contains("/Count 1", pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("This plan cannot be calculated:"), pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateSeries_ShouldTitleTheSeriesWithThePlansNameOrAFallback_WhenThePlanIsNamed()
    {
        // Arrange
        var plan = TestFactory.SeriesPlan();
        var unnamed = plan with { Name = " " };

        // Act
        var named = Series(plan);
        var fallback = Series(unnamed);

        // Assert
        Assert.Contains("/Title (Reef)", named, StringComparison.Ordinal);
        Assert.Contains("/Title (Dive plan)", fallback, StringComparison.Ordinal);
    }
}
