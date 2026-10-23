using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionCalculator.Export.Pdf;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Export;

/// <summary>
/// Provides a method for exporting one dive of a plan as a printable one-page A4 PDF dive sheet.
/// </summary>
public static class DiveSheet
{
    /// <summary>
    /// The warning at the foot of the dive sheet, which also ends the text summary.
    /// </summary>
    public static string Disclaimer => ExportText.Disclaimer;

    private const double Gap = 10;
    private const double FigureHeight = 30;
    private const double GasRowHeight = 14;
    private const double OverviewRowHeight = 18;
    private const double LargestRow = 16;
    private const double SmallestRowBesideChart = 11;
    private const double SmallestRow = 7;
    private const double SmallestChart = 120;
    private const double LargestChart = 230;

    private static readonly PdfColor SafetyStopColor = PdfColor.FromRgb(232, 137, 12);
    private static readonly PdfColor SwitchColor = PdfColor.FromRgb(28, 128, 67);
    private static readonly PdfColor DiveFill = PdfColor.FromRgb(228, 238, 248);
    private static readonly PdfColor CeilingZone = PdfColor.FromRgb(208, 156, 162);

    /// <summary>
    /// Returns one dive of the plan as the bytes of a PDF file.
    /// </summary>
    /// <param name="plan">The plan, which is calculated for the sheet.</param>
    /// <param name="dive">The zero-based index of the dive to export.</param>
    /// <param name="exportedAt">When the sheet is made, in UTC; printed on the sheet and stored as its creation time.</param>
    /// <param name="appVersion">The version of the app that made the sheet, printed beside its title.</param>
    /// <param name="blocks">
    /// The result blocks to show, in order; <see langword="null"/> for every block in the default order.
    /// </param>
    /// <param name="mergeStops">Whether each ascent or descent that ends in a stop is shown as part of that stop.</param>
    /// <returns>The PDF file.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="plan"/> or <paramref name="appVersion"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dive"/> is not a dive of the plan.</exception>
    /// <remarks>
    /// <para>
    /// Warnings come first, then the blocks in the order given. They are sized to fit one page: the profile's rows
    /// shrink first, no further than a size worth reading beside the chart, and the chart then takes what is left,
    /// between a smallest and a largest height. A block that does not fit moves to the next page, so none is ever left out.
    /// </para>
    /// <para>
    /// A plan with issues is exported with the issues listed instead of its results.
    /// </para>
    /// </remarks>
    public static byte[] Create(
        DivePlanDocument plan, int dive, DateTime exportedAt, string appVersion, IReadOnlyList<ResultBlock>? blocks = null, bool mergeStops = false)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(appVersion);
        ArgumentOutOfRangeException.ThrowIfNegative(dive);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(dive, plan.Dives.Count);

        var calculation = PlanCalculator.Calculate(plan);
        var sheet = NewSheet();
        WriteDiveSheet(sheet, plan, calculation, dive, exportedAt, appVersion, blocks ?? UserPreferences.Default.VisibleBlocks(), mergeStops);
        return sheet.Build(Title(plan), exportedAt);
    }

    /// <summary>
    /// Returns every dive of the plan as the bytes of one PDF file: a page that lists the series, then each dive as
    /// <see cref="Create"/> lays it out, starting on a page of its own.
    /// </summary>
    /// <param name="plan">The plan, which is calculated for the sheets.</param>
    /// <param name="exportedAt">When the sheets are made, in UTC; printed on them and stored as the file's creation time.</param>
    /// <param name="appVersion">The version of the app that made the sheets, printed beside each title.</param>
    /// <param name="blocks">
    /// The result blocks to show on each dive, in order; <see langword="null"/> for every block in the default order.
    /// </param>
    /// <param name="mergeStops">Whether each ascent or descent that ends in a stop is shown as part of that stop, on each dive.</param>
    /// <returns>The PDF file.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="plan"/> or <paramref name="appVersion"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// A plan with issues is exported as its first page alone, with the issues listed, since no dive can be shown.
    /// </remarks>
    public static byte[] CreateSeries(
        DivePlanDocument plan, DateTime exportedAt, string appVersion, IReadOnlyList<ResultBlock>? blocks = null, bool mergeStops = false)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(appVersion);

        var calculation = PlanCalculator.Calculate(plan);
        var sheet = NewSheet();
        WriteOverview(sheet, plan, calculation, exportedAt, appVersion);

        if (calculation.IsCalculated)
        {
            for (var dive = 0; dive < plan.Dives.Count; dive++)
            {
                sheet.NewPage();
                WriteDiveSheet(sheet, plan, calculation, dive, exportedAt, appVersion, blocks ?? UserPreferences.Default.VisibleBlocks(), mergeStops);
            }
        }

        return sheet.Build(Title(plan), exportedAt);
    }

    private static SheetWriter NewSheet()
    {
        var sheet = new SheetWriter();
        sheet.Footnote(Disclaimer, PdfFont.HelveticaBold, 7.5, SheetWriter.Text);
        return sheet;
    }

    private static string Title(DivePlanDocument plan) => string.IsNullOrWhiteSpace(plan.Name) ? ExportText.DivePlan : plan.Name;

    private static void WriteDiveSheet(
        SheetWriter sheet,
        DivePlanDocument plan,
        PlanCalculation calculation,
        int dive,
        DateTime exportedAt,
        string appVersion,
        IReadOnlyList<ResultBlock> blocks,
        bool mergeStops)
    {
        WriteTitle(sheet, Title(plan), Localized.Format(ExportText.SheetMeta, Which(plan, dive), Summary.Date(exportedAt), appVersion));
        sheet.Paragraph($"{Summary.Algorithm(plan)} · {Summary.Settings(plan, plan.SettingsFor(dive))}", PdfFont.Helvetica, 7.5, SheetWriter.Muted);

        var input = plan.Dives[dive];
        var interval = dive > 0 ? Localized.Format(ExportText.SheetInterval, Formats.HoursAndMinutes(TimeSpan.FromMinutes(input.SurfaceIntervalMinutes))) : string.Empty;
        sheet.Paragraph(interval + Summary.Inputs(input, plan.Units), PdfFont.Helvetica, 8.5, SheetWriter.Text);

        if (!calculation.IsCalculated)
        {
            WriteIssues(sheet, calculation);
        }
        else
        {
            var labels = UnitLabels.For(plan.Units);
            var result = Shown(calculation.Dives[dive], mergeStops);
            List<ResultBlock> shown = [.. blocks.Where(static block => block != ResultBlock.Contingencies)];
            WriteDive(sheet, plan, result, labels, shown, plan.SettingsFor(dive).GasQuantity);

            if (blocks.Contains(ResultBlock.Contingencies))
            {
                foreach (var contingency in result.Contingencies)
                {
                    sheet.NewPage();
                    WriteTitle(sheet, Title(plan), Localized.Format(ExportText.ContingencySheetMeta, Which(plan, dive), Summary.Date(exportedAt), appVersion));
                    sheet.Paragraph(Summary.ContingencyName(contingency, labels), PdfFont.HelveticaBold, 10, SheetWriter.Text);
                    sheet.Paragraph(ContingencyMeaning(contingency, labels), PdfFont.Helvetica, 8.5, SheetWriter.Muted);
                    sheet.Paragraph($"{Summary.Algorithm(plan)} · {Summary.Settings(plan, plan.SettingsFor(dive))}", PdfFont.Helvetica, 7.5, SheetWriter.Muted);
                    sheet.Paragraph(interval + Summary.Inputs(input, plan.Units), PdfFont.Helvetica, 8.5, SheetWriter.Text);
                    WriteDive(sheet, plan, Shown(contingency.Result, mergeStops), labels, shown, plan.SettingsFor(dive).GasQuantity);
                }
            }
        }
    }

    /// <remarks>Merging only changes the profile's rows; the figures are the same.</remarks>
    private static DiveResult Shown(DiveResult result, bool mergeStops) => mergeStops ? result.WithStopsMerged() : result;

    private static void WriteIssues(SheetWriter sheet, PlanCalculation calculation)
    {
        sheet.Advance(Gap);
        sheet.Paragraph(ExportText.CannotBeCalculated, PdfFont.HelveticaBold, 11, SheetWriter.Danger);
        foreach (var issue in calculation.Issues)
        {
            sheet.Paragraph(Localized.Format(ExportText.IssueLine, Summary.Describe(issue)), PdfFont.Helvetica, 10, SheetWriter.Text);
        }
    }

    /// <remarks>
    /// Like the series table on screen: each dive with its surface interval and, from the plan, its depth, runtime,
    /// time to surface, and the CNS and oxygen tolerance units that include the dives before it.
    /// </remarks>
    private static void WriteOverview(SheetWriter sheet, DivePlanDocument plan, PlanCalculation calculation, DateTime exportedAt, string appVersion)
    {
        var labels = UnitLabels.For(plan.Units);
        WriteTitle(sheet, Title(plan), Localized.Format(ExportText.SeriesMeta, plan.Dives.Count, Summary.Date(exportedAt), appVersion));
        sheet.Paragraph($"{Summary.Algorithm(plan)} · {Summary.Settings(plan)}", PdfFont.Helvetica, 7.5, SheetWriter.Muted);

        if (!calculation.IsCalculated)
        {
            WriteIssues(sheet, calculation);
            return;
        }

        sheet.Advance(Gap);
        SheetColumn[] columns =
        [
            new(ExportText.ColumnDive, 141), new(ExportText.ColumnInterval, 60, PdfAlign.Right), new(Localized.Format(ExportText.ColumnDepth, labels.Depth), 70, PdfAlign.Right),
            new(ExportText.ColumnRuntime, 80, PdfAlign.Right, Emphasis: true), new(ExportText.ColumnTts, 60, PdfAlign.Right),
            new(ExportText.ColumnCns, 60, PdfAlign.Right), new(ExportText.ColumnOtu, 60, PdfAlign.Right)
        ];
        var rows = plan.Dives
            .Select((dive, index) =>
            {
                var result = calculation.Dives[index];
                var interval = index == 0 ? "–" : Formats.HoursAndMinutes(TimeSpan.FromMinutes(dive.SurfaceIntervalMinutes));
                return result.Succeeded
                    ? new[]
                    {
                        dive.DisplayName(index + 1), interval, Formats.Number(result.MaximumDepth), Formats.Clock(result.TotalRuntime),
                        Formats.Clock(result.TimeToSurface), Formats.Number(result.CnsPercent), Formats.Number(result.OxygenToleranceUnits)
                    }
                    : [dive.DisplayName(index + 1), interval, ExportText.CannotBePlanned, string.Empty, string.Empty, string.Empty, string.Empty];
            })
            .ToArray();

        sheet.Table(columns, rows, _ => false, OverviewRowHeight, 9);
        sheet.Advance(Gap / 2);
        sheet.Paragraph(
            ExportText.SeriesNote,
            PdfFont.Helvetica, 7.5, SheetWriter.Muted);
    }

    /// <remarks>The title and the line beside it share a line when both fit, and stack otherwise.</remarks>
    private static void WriteTitle(SheetWriter sheet, string title, string meta)
    {
        const double titleSize = 14;
        const double metaSize = 8;
        var fits = PdfFontMetrics.Measure(title, PdfFont.HelveticaBold, titleSize)
            + PdfFontMetrics.Measure(meta, PdfFont.Helvetica, metaSize) + 16 <= SheetWriter.ContentWidth;

        if (fits)
        {
            sheet.Page.Text(SheetWriter.Margin, sheet.Y + titleSize, title, PdfFont.HelveticaBold, titleSize, SheetWriter.Text);
            sheet.Page.Text(SheetWriter.Margin + SheetWriter.ContentWidth, sheet.Y + titleSize, meta, PdfFont.Helvetica, metaSize,
                SheetWriter.Muted, PdfAlign.Right);
            sheet.Advance(titleSize * 1.5);
        }
        else
        {
            sheet.Paragraph(title, PdfFont.HelveticaBold, titleSize, SheetWriter.Text);
            sheet.Paragraph(meta, PdfFont.Helvetica, metaSize, SheetWriter.Muted);
        }
    }

    private static void WriteDive(
        SheetWriter sheet, DivePlanDocument plan, DiveResult result, UnitLabels labels, IReadOnlyList<ResultBlock> blocks, GasQuantity quantity)
    {
        sheet.Advance(Gap / 2);
        if (result.FailureMessage is { } failure)
        {
            sheet.Paragraph(failure, PdfFont.HelveticaBold, 10, SheetWriter.Danger);
            return;
        }

        WriteWarnings(sheet, result, labels, plan.Units, quantity);

        var pages = Paginate(blocks, result, sheet.Remaining, sheet.Capacity);
        for (var index = 0; index < pages.Count; index++)
        {
            if (index > 0)
            {
                sheet.NewPage();
            }

            WritePage(sheet, plan, result, labels, pages[index], quantity);
        }
    }

    /// <remarks>
    /// Blocks keep their order. A block that does not fit at its smallest, with the others already on the page,
    /// starts the next page; a block taller than a page of its own stays where it is and runs off it.
    /// </remarks>
    private static List<List<ResultBlock>> Paginate(
        IReadOnlyList<ResultBlock> blocks, DiveResult result, double firstPage, double laterPages)
    {
        var pages = new List<List<ResultBlock>> { new() };
        var capacity = firstPage;

        foreach (var block in blocks)
        {
            var page = pages[^1];
            if (page.Count > 0 && Smallest([.. page, block], result) > capacity)
            {
                pages.Add([block]);
                capacity = laterPages;
            }
            else
            {
                page.Add(block);
            }
        }

        return pages;
    }

    private static void WritePage(SheetWriter sheet, DivePlanDocument plan, DiveResult result, UnitLabels labels, List<ResultBlock> blocks, GasQuantity quantity)
    {
        var spare = Math.Max(0, sheet.Remaining - Smallest(blocks, result));
        var rows = result.Profile.Count + 1;
        var rowHeight = SmallestRowOn(blocks);
        if (blocks.Contains(ResultBlock.Profile))
        {
            var larger = Math.Min(rowHeight + spare / rows, LargestRow);
            spare -= (larger - rowHeight) * rows;
            rowHeight = larger;
        }

        var chartHeight = Math.Min(SmallestChart + spare, LargestChart);

        for (var index = 0; index < blocks.Count; index++)
        {
            if (index > 0)
            {
                sheet.Advance(Gap);
            }

            switch (blocks[index])
            {
                case ResultBlock.Figures:
                    WriteFigures(sheet, result);
                    break;
                case ResultBlock.Profile:
                    WriteProfile(sheet, result, labels, rowHeight);
                    break;
                case ResultBlock.Gas:
                    WriteGas(sheet, result, labels, plan.Units, quantity);
                    break;
                case ResultBlock.GasChecks:
                    WriteGasChecks(sheet, result, labels);
                    break;
                case ResultBlock.Chart:
                    WriteChart(sheet, ProfileChart.Create(result, plan.Units), labels, chartHeight);
                    break;
            }
        }
    }

    /// <remarks>
    /// The profile is read underwater, so beside the chart its rows keep a size worth reading; on a page of its
    /// own they may shrink further.
    /// </remarks>
    private static double SmallestRowOn(List<ResultBlock> blocks) =>
        blocks.Contains(ResultBlock.Chart) ? SmallestRowBesideChart : SmallestRow;

    private static double Smallest(List<ResultBlock> blocks, DiveResult result) =>
        blocks.Sum(block => block switch
        {
            ResultBlock.Figures => FigureHeight,
            ResultBlock.Profile => (result.Profile.Count + 1) * SmallestRowOn(blocks),
            ResultBlock.Gas => (result.Cylinders.Count + 1) * GasRowHeight,
            ResultBlock.GasChecks => (result.GasChecks.Count + 1) * GasRowHeight,
            ResultBlock.Chart => SmallestChart,
            _ => 0
        }) + Gap * Math.Max(0, blocks.Count - 1);

    private static void WriteFigures(SheetWriter sheet, DiveResult result)
    {
        (string Label, string Value)[] figures =
        [
            (ExportText.FigureRuntime, Formats.Minutes(result.TotalRuntime)),
            (ExportText.FigureTts, Formats.Minutes(result.TimeToSurface)),
            (ExportText.FigureCns, Localized.Format(ExportText.Percent, Formats.Number(result.CnsPercent))),
            (ExportText.FigureOtu, Formats.Number(result.OxygenToleranceUnits))
        ];

        const double gap = 6;
        var width = (SheetWriter.ContentWidth - gap * (figures.Length - 1)) / figures.Length;
        for (var index = 0; index < figures.Length; index++)
        {
            var left = SheetWriter.Margin + index * (width + gap);
            sheet.Page.Rectangle(left, sheet.Y, width, FigureHeight, SheetWriter.Sunken);
            sheet.Page.Text(left + 6, sheet.Y + 10, figures[index].Label, PdfFont.Helvetica, 7, SheetWriter.Muted);
            sheet.Page.Text(left + 6, sheet.Y + 24, figures[index].Value, PdfFont.HelveticaBold, 11, SheetWriter.Text);
        }

        sheet.Advance(FigureHeight);
    }

    private static void WriteWarnings(SheetWriter sheet, DiveResult result, UnitLabels labels, UnitSystem units, GasQuantity quantity)
    {
        foreach (var violation in result.Violations)
        {
            sheet.Paragraph(
                Localized.Format(ExportText.ViolationWarning, Formats.Number(violation.FromDepth), Formats.Number(violation.ToDepth), labels.Depth,
                    Formats.Number(violation.Ceiling, 1)),
                PdfFont.HelveticaBold, 8.5, SheetWriter.Danger);
        }

        foreach (var cylinder in result.Cylinders.Where(static cylinder => cylinder.MissingLitres > 0))
        {
            sheet.Paragraph(
                Localized.Format(ExportText.RunsOutWarning, cylinder.Gas, Formats.Number(GasAmounts.For(cylinder, quantity, units).Missing),
                    quantity == GasQuantity.Pressure ? labels.Pressure : labels.GasVolume),
                PdfFont.HelveticaBold, 8.5, SheetWriter.Danger);
        }

        if (!result.ReserveSatisfied)
        {
            sheet.Paragraph(ExportText.ReserveWarning, PdfFont.HelveticaBold, 8.5, SheetWriter.Danger);
        }

        foreach (var warning in GasWarnings.For(result, units))
        {
            sheet.Paragraph(warning, PdfFont.HelveticaBold, 8.5, SheetWriter.Danger);
        }
    }

    /// <remarks>Depth and runtime are what a diver reads off the sheet underwater, so they are bold.</remarks>
    private static void WriteProfile(SheetWriter sheet, DiveResult result, UnitLabels labels, double rowHeight)
    {
        var showsApparatus = result.Profile.Any(static phase => phase.Circuit != BreathingLoop.OpenCircuit.ToString());
        SheetColumn[] columns =
        [
            new("", 80), new(Localized.Format(ExportText.ColumnDepth, labels.Depth), 80, PdfAlign.Right, Emphasis: true), new(ExportText.ColumnTime, 80, PdfAlign.Right),
            new(ExportText.ColumnRuntime, 90, PdfAlign.Right, Emphasis: true), new(ExportText.ColumnGas, 90), new(showsApparatus ? ExportText.ColumnApparatus : "", 111)
        ];
        var rows = result.Profile
            .Select(phase => new[]
            {
                Names.Of(phase.Kind), Formats.Number(phase.EndDepth), Formats.Clock(phase.Duration), Formats.Clock(phase.Runtime),
                phase.Gas, showsApparatus ? phase.Circuit : string.Empty
            })
            .ToArray();

        sheet.Table(columns, rows, row => result.Profile[row].Kind is SegmentKind.Stop or SegmentKind.SafetyStop or SegmentKind.GasSwitch,
            rowHeight, Math.Clamp(rowHeight * 0.62, 6.5, 10));
    }

    private static void WriteGas(SheetWriter sheet, DiveResult result, UnitLabels labels, UnitSystem units, GasQuantity quantity)
    {
        var unit = quantity == GasQuantity.Pressure ? labels.Pressure : labels.GasVolume;
        var missing = result.Cylinders.Any(static cylinder => cylinder.MissingLitres > 0);
        SheetColumn[] columns = missing
            ?
            [
                new(ExportText.ColumnGas, 50), new(ExportText.ColumnUsedAs, 75), new(Localized.Format(ExportText.ColumnStart, unit), 60, PdfAlign.Right),
                new(Localized.Format(ExportText.ColumnFinish, unit), 58, PdfAlign.Right), new(Localized.Format(ExportText.ColumnUsed, unit), 62, PdfAlign.Right),
                new(Localized.Format(ExportText.ColumnReserve, unit), 62, PdfAlign.Right), new(Localized.Format(ExportText.ColumnLeft, unit), 58, PdfAlign.Right),
                new(Localized.Format(ExportText.ColumnMissing, unit), 62, PdfAlign.Right)
            ]
            :
            [
                new(ExportText.ColumnGas, 60), new(ExportText.ColumnUsedAs, 85), new(Localized.Format(ExportText.ColumnStart, unit), 70, PdfAlign.Right),
                new(Localized.Format(ExportText.ColumnFinish, unit), 68, PdfAlign.Right), new(Localized.Format(ExportText.ColumnUsed, unit), 70, PdfAlign.Right),
                new(Localized.Format(ExportText.ColumnReserve, unit), 70, PdfAlign.Right), new(Localized.Format(ExportText.ColumnLeft, unit), 70, PdfAlign.Right)
            ];
        var rows = result.Cylinders
            .Select(cylinder =>
            {
                var amount = GasAmounts.For(cylinder, quantity, units);
                return new[]
                    {
                        cylinder.Gas, Names.Of(cylinder.Purpose), Formats.Number(amount.Start), Formats.Number(amount.End),
                        Formats.Number(amount.Used), Formats.Number(amount.Reserve), Formats.Number(amount.Left)
                    }
                    .Concat(missing ? [Formats.Number(amount.Missing)] : [])
                    .ToArray();
            })
            .ToArray();

        sheet.Table(columns, rows, _ => false, GasRowHeight, 8.5);
    }

    private static string ContingencyMeaning(Contingency contingency, UnitLabels labels) => contingency.Kind == ContingencyKind.LostGas
        ? Localized.Format(ExportText.ContingencyLostMeaning, contingency.LostGas)
        : Localized.Format(ExportText.ContingencyExtendedMeaning, Formats.Number(contingency.ExtraDepth), labels.Depth, Formats.Number(contingency.ExtraMinutes));

    private static void WriteGasChecks(SheetWriter sheet, DiveResult result, UnitLabels labels)
    {
        SheetColumn[] columns =
        [
            new(ExportText.ColumnGas, 90), new(Localized.Format(ExportText.ColumnDepth, labels.Depth), 95, PdfAlign.Right), new(ExportText.ColumnPo2, 100, PdfAlign.Right),
            new(Localized.Format(ExportText.ColumnEnd, labels.Depth), 105, PdfAlign.Right), new(ExportText.ColumnDensity, 141, PdfAlign.Right, Emphasis: true)
        ];
        var rows = result.GasChecks
            .Select(static check => new[]
            {
                check.Gas, Formats.Number(check.Depth), Formats.Number(check.OxygenPressure, 2),
                Formats.Number(check.EquivalentNarcoticDepth), Formats.Number(check.Density, 1)
            })
            .ToArray();

        sheet.Table(columns, rows, row => result.GasChecks[row].IsTooDense, GasRowHeight, 8.5);
    }

    private static void WriteChart(SheetWriter sheet, ProfileChart chart, UnitLabels labels, double height)
    {
        var page = sheet.Page;
        var area = new ChartArea(SheetWriter.Margin + 30, sheet.Y + 12, SheetWriter.ContentWidth - 34, height - 36);

        foreach (var depth in chart.DepthTicks)
        {
            page.Text(area.Left - 5, chart.Y(depth, area) + 3, Formats.Number(depth), PdfFont.Helvetica, 7, SheetWriter.Muted, PdfAlign.Right);
        }

        foreach (var minutes in chart.TimeTicks)
        {
            page.Text(chart.X(minutes, area), area.Top + area.Height + 10, Formats.Number(minutes), PdfFont.Helvetica, 7, SheetWriter.Muted, PdfAlign.Center);
        }

        var points = chart.Points.Select(point => (chart.X(point.Minutes, area), chart.Y(point.Depth, area))).ToArray();
        page.Polygon(points, DiveFill);

        var ceiling = chart.Ceiling.Select(point => (chart.X(point.Minutes, area), chart.Y(point.Depth, area))).ToArray();
        var surface = chart.Y(0, area);
        if (ceiling.Length > 0)
        {
            page.Polygon([.. ceiling, (ceiling[^1].Item1, surface), (ceiling[0].Item1, surface)], CeilingZone);
        }

        page.Polyline(points, 3, SheetWriter.Brand);

        if (ceiling.Length > 0)
        {
            page.Polyline(ceiling, 2.2, SheetWriter.Danger);
        }

        foreach (var stop in chart.Stops)
        {
            var y = chart.Y(stop.Depth, area);
            var start = chart.X(stop.StartMinutes, area);
            page.Line(start, y, chart.X(stop.EndMinutes, area), y, stop.IsGasSwitch ? 5 : 3, StopColor(stop));
            if (stop.IsGasSwitch)
            {
                page.Polygon([.. Enumerable.Range(0, 16).Select(step => (start + 4.5 * Math.Cos(step * Math.PI / 8), y + 4.5 * Math.Sin(step * Math.PI / 8)))], SwitchColor);
                page.Text(start + 7, y - 8, stop.Gas, PdfFont.HelveticaBold, 7, SwitchColor);
            }
        }

        page.Text(area.Left + 3, area.Top - 3, labels.Depth, PdfFont.Helvetica, 7, SheetWriter.Muted);
        page.Text(area.Left + area.Width, area.Top + area.Height + 20, ExportText.MinutesAxis, PdfFont.Helvetica, 7, SheetWriter.Muted, PdfAlign.Right);
        sheet.Advance(height);
    }

    /// <remarks>
    /// A dive of a series also gives its place in it, so a named dive still says which one it is.
    /// </remarks>
    private static string Which(DivePlanDocument plan, int dive)
    {
        var name = plan.Dives[dive].Name?.Trim();
        var place = Localized.Format(ExportText.PlaceOf, dive + 1, plan.Dives.Count);
        if (string.IsNullOrEmpty(name))
        {
            return plan.Dives.Count == 1 ? ExportText.Dive : Localized.Format(ExportText.DiveNumbered, place);
        }

        return plan.Dives.Count == 1 ? name : Localized.Format(ExportText.NamedPlace, name, place);
    }

    private static PdfColor StopColor(ChartStop stop) => stop switch
    {
        { IsGasSwitch: true } => SwitchColor,
        { IsSafetyStop: true } => SafetyStopColor,
        _ => SheetWriter.Danger
    };
}
