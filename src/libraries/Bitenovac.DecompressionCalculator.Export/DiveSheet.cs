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
    public const string Disclaimer =
        "Warning: Bitenovac is decompression planning software that has had only limited testing. " +
        "Do not dive a plan based on these results alone: check it against other planning tools, " +
        "follow your dive computer, and dive within your training and certification.";

    private const double Gap = 10;
    private const double FigureHeight = 30;
    private const double GasRowHeight = 14;
    private const double LargestRow = 16;
    private const double SmallestRowBesideChart = 11;
    private const double SmallestRow = 7;
    private const double SmallestChart = 120;
    private const double LargestChart = 230;

    private static readonly PdfColor SafetyStopColor = PdfColor.FromRgb(232, 137, 12);
    private static readonly PdfColor SwitchColor = PdfColor.FromRgb(28, 128, 67);
    private static readonly PdfColor Water = PdfColor.FromRgb(238, 245, 252);
    private static readonly PdfColor DiveFill = PdfColor.FromRgb(214, 230, 245);
    private static readonly PdfColor CeilingZone = PdfColor.FromRgb(240, 201, 201);

    /// <summary>
    /// Returns one dive of the plan as the bytes of a one-page PDF file.
    /// </summary>
    /// <param name="plan">The plan, which is calculated for the sheet.</param>
    /// <param name="dive">The zero-based index of the dive to export.</param>
    /// <param name="exportedAt">When the sheet is made, in UTC; printed on the sheet and stored as its creation time.</param>
    /// <param name="appVersion">The version of the app that made the sheet, printed beside its title.</param>
    /// <returns>The PDF file.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="plan"/> or <paramref name="appVersion"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dive"/> is not a dive of the plan.</exception>
    /// <remarks>
    /// <para>
    /// The profile and the gas come first, at a size that keeps them on the page; the chart fills the
    /// space left below them, and is left out when there is too little.
    /// </para>
    /// <para>
    /// A plan with issues is exported with the issues listed instead of its results.
    /// </para>
    /// </remarks>
    public static byte[] Create(DivePlanDocument plan, int dive, DateTime exportedAt, string appVersion)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(appVersion);
        ArgumentOutOfRangeException.ThrowIfNegative(dive);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(dive, plan.Dives.Count);

        var calculation = PlanCalculator.Calculate(plan);
        var labels = UnitLabels.For(plan.Units);
        var sheet = new SheetWriter();
        sheet.Footnote(Disclaimer, PdfFont.HelveticaBold, 7.5, SheetWriter.Text);
        var title = string.IsNullOrWhiteSpace(plan.Name) ? "Dive plan" : plan.Name;

        var which = Which(plan, dive);
        WriteTitle(sheet, title, $"{which} · {Summary.Date(exportedAt)} UTC · Bitenovac {appVersion}");
        sheet.Paragraph($"{Summary.Algorithm(plan)} · {Summary.Settings(plan)}", PdfFont.Helvetica, 7.5, SheetWriter.Muted);

        var input = plan.Dives[dive];
        var interval = dive > 0 ? $"After {Formats.Number(input.SurfaceIntervalMinutes)} min on the surface · " : string.Empty;
        sheet.Paragraph(interval + Summary.Inputs(input, plan.Units), PdfFont.Helvetica, 8.5, SheetWriter.Text);

        if (!calculation.IsCalculated)
        {
            sheet.Advance(Gap);
            sheet.Paragraph("This plan cannot be calculated:", PdfFont.HelveticaBold, 11, SheetWriter.Danger);
            foreach (var issue in calculation.Issues)
            {
                sheet.Paragraph($"- {Summary.Describe(issue)}", PdfFont.Helvetica, 10, SheetWriter.Text);
            }
        }
        else
        {
            WriteDive(sheet, plan, calculation.Dives[dive], labels);
        }

        return sheet.Build(title, exportedAt);
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

    private static void WriteDive(SheetWriter sheet, DivePlanDocument plan, DiveResult result, UnitLabels labels)
    {
        sheet.Advance(Gap / 2);
        if (result.FailureMessage is { } failure)
        {
            sheet.Paragraph(failure, PdfFont.HelveticaBold, 10, SheetWriter.Danger);
            return;
        }

        WriteFigures(sheet, result);
        WriteWarnings(sheet, result, labels);

        var profileRows = result.Profile.Count + 1;
        var gasHeight = (result.Cylinders.Count + 1) * GasRowHeight;
        var besideChart = (sheet.Remaining - gasHeight - 2 * Gap - SmallestChart) / profileRows;
        var withChart = besideChart >= SmallestRowBesideChart;
        var rowHeight = withChart
            ? Math.Min(besideChart, LargestRow)
            : Math.Clamp((sheet.Remaining - gasHeight - Gap) / profileRows, SmallestRow, LargestRow);

        WriteProfile(sheet, result, labels, rowHeight);
        sheet.Advance(Gap);
        WriteGas(sheet, result, labels);

        if (withChart)
        {
            sheet.Advance(Gap);
            WriteChart(sheet, ProfileChart.Create(result, plan.Units), labels, Math.Min(sheet.Remaining, LargestChart));
        }
    }

    private static void WriteFigures(SheetWriter sheet, DiveResult result)
    {
        (string Label, string Value)[] figures =
        [
            ("Runtime", Formats.Minutes(result.TotalRuntime)),
            ("TTS", Formats.Minutes(TimeToSurface(result))),
            ("CNS", $"{Formats.Number(result.CnsPercent)} %"),
            ("OTU", Formats.Number(result.OxygenToleranceUnits))
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

        sheet.Advance(FigureHeight + Gap / 2);
    }

    private static void WriteWarnings(SheetWriter sheet, DiveResult result, UnitLabels labels)
    {
        foreach (var violation in result.Violations)
        {
            sheet.Paragraph(
                $"Ascent from {Formats.Number(violation.FromDepth)} to {Formats.Number(violation.ToDepth)} {labels.Depth} " +
                $"passes the decompression ceiling at {Formats.Number(violation.Ceiling, 1)} {labels.Depth}.",
                PdfFont.HelveticaBold, 8.5, SheetWriter.Danger);
        }

        foreach (var cylinder in result.Cylinders.Where(static cylinder => cylinder.Shortfall > 0))
        {
            sheet.Paragraph(
                $"{cylinder.Gas} runs out: the dive needs {Formats.Number(cylinder.Shortfall)} {labels.GasVolume} more than the cylinder holds.",
                PdfFont.HelveticaBold, 8.5, SheetWriter.Danger);
        }

        if (!result.ReserveSatisfied)
        {
            sheet.Paragraph("A cylinder does not hold the reserve gas the plan requires.", PdfFont.HelveticaBold, 8.5, SheetWriter.Danger);
        }
    }

    /// <remarks>Depth and runtime are what a diver reads off the sheet underwater, so they are bold.</remarks>
    private static void WriteProfile(SheetWriter sheet, DiveResult result, UnitLabels labels, double rowHeight)
    {
        var showsApparatus = result.Profile.Any(static phase => phase.Circuit != BreathingLoop.OpenCircuit.ToString());
        SheetColumn[] columns =
        [
            new("", 80), new($"Depth ({labels.Depth})", 80, PdfAlign.Right, Emphasis: true), new("Time (min)", 80, PdfAlign.Right),
            new("Runtime (min)", 90, PdfAlign.Right, Emphasis: true), new("Gas", 90), new(showsApparatus ? "Apparatus" : "", 111)
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

    private static void WriteGas(SheetWriter sheet, DiveResult result, UnitLabels labels)
    {
        var missing = result.Cylinders.Any(static cylinder => cylinder.Shortfall > 0);
        SheetColumn[] columns = missing
            ?
            [
                new("Gas", 60), new("Used as", 85), new($"Pressure ({labels.Pressure})", 90, PdfAlign.Right),
                new($"Used ({labels.GasVolume})", 72, PdfAlign.Right), new($"Reserve ({labels.GasVolume})", 72, PdfAlign.Right),
                new($"Left ({labels.GasVolume})", 72, PdfAlign.Right), new($"Missing ({labels.GasVolume})", 80, PdfAlign.Right)
            ]
            :
            [
                new("Gas", 70), new("Used as", 95), new($"Pressure ({labels.Pressure})", 100, PdfAlign.Right),
                new($"Used ({labels.GasVolume})", 85, PdfAlign.Right), new($"Reserve ({labels.GasVolume})", 85, PdfAlign.Right),
                new($"Left ({labels.GasVolume})", 85, PdfAlign.Right)
            ];
        var rows = result.Cylinders
            .Select(cylinder => new[]
                {
                    cylinder.Gas, Names.Of(cylinder.Purpose),
                    $"{Formats.Number(cylinder.StartPressure)} to {Formats.Number(cylinder.EndPressure)}",
                    Formats.Number(cylinder.GasUsed), Formats.Number(cylinder.RequiredReserve), Formats.Number(cylinder.ProjectedRemaining)
                }
                .Concat(missing ? [Formats.Number(cylinder.Shortfall)] : [])
                .ToArray())
            .ToArray();

        sheet.Table(columns, rows, _ => false, GasRowHeight, 8.5);
    }

    private static void WriteChart(SheetWriter sheet, ProfileChart chart, UnitLabels labels, double height)
    {
        var page = sheet.Page;
        var area = new ChartArea(SheetWriter.Margin + 30, sheet.Y + 12, SheetWriter.ContentWidth - 34, height - 36);

        page.Rectangle(area.Left, area.Top, area.Width, area.Height, Water);
        foreach (var depth in chart.DepthTicks)
        {
            var y = chart.Y(depth, area);
            page.Line(area.Left, y, area.Left + area.Width, y, 0.4, SheetWriter.Rule);
            page.Text(area.Left - 5, y + 3, Formats.Number(depth), PdfFont.Helvetica, 7, SheetWriter.Muted, PdfAlign.Right);
        }

        foreach (var minutes in chart.TimeTicks)
        {
            var x = chart.X(minutes, area);
            page.Line(x, area.Top, x, area.Top + area.Height, 0.4, SheetWriter.Rule);
            page.Text(x, area.Top + area.Height + 10, Formats.Number(minutes), PdfFont.Helvetica, 7, SheetWriter.Muted, PdfAlign.Center);
        }

        var points = chart.Points.Select(point => (chart.X(point.Minutes, area), chart.Y(point.Depth, area))).ToArray();
        page.Polygon(points, DiveFill);

        var ceiling = chart.Ceiling.Select(point => (chart.X(point.Minutes, area), chart.Y(point.Depth, area))).ToArray();
        if (ceiling.Length > 0)
        {
            page.Polygon([.. ceiling, (ceiling[^1].Item1, area.Top), (ceiling[0].Item1, area.Top)], CeilingZone);
        }

        page.Polyline(points, 1.6, SheetWriter.Brand);

        if (ceiling.Length > 0)
        {
            page.Polyline(ceiling, 1.2, SheetWriter.Danger);
        }

        foreach (var stop in chart.Stops)
        {
            var y = chart.Y(stop.Depth, area);
            page.Line(chart.X(stop.StartMinutes, area), y, chart.X(stop.EndMinutes, area), y, 1.6,
                StopColor(stop));
            if (stop.IsGasSwitch)
            {
                page.Text(chart.X(stop.StartMinutes, area) + 3, y - 5, stop.Gas, PdfFont.HelveticaBold, 7, SwitchColor);
            }
        }

        page.Text(area.Left + 3, area.Top - 3, labels.Depth, PdfFont.Helvetica, 7, SheetWriter.Muted);
        page.Text(area.Left + area.Width, area.Top + area.Height + 20, "min", PdfFont.Helvetica, 7, SheetWriter.Muted, PdfAlign.Right);
        sheet.Advance(height);
    }

    /// <remarks>
    /// A dive of a series also gives its place in it, so a named dive still says which one it is.
    /// </remarks>
    private static string Which(DivePlanDocument plan, int dive)
    {
        var name = plan.Dives[dive].Name?.Trim();
        var place = $"{dive + 1} of {plan.Dives.Count}";
        if (string.IsNullOrEmpty(name))
        {
            return plan.Dives.Count == 1 ? "Dive" : $"Dive {place}";
        }

        return plan.Dives.Count == 1 ? name : $"{name} ({place})";
    }

    private static PdfColor StopColor(ChartStop stop) => stop switch
    {
        { IsGasSwitch: true } => SwitchColor,
        { IsSafetyStop: true } => SafetyStopColor,
        _ => SheetWriter.Danger
    };

    /// <remarks>
    /// The ascent starts after the last level; a level of zero minutes has no phase of its own, so after the
    /// last level or descent.
    /// </remarks>
    private static TimeSpan TimeToSurface(DiveResult result) => result.TotalRuntime
        - result.Profile.Last(static phase => phase.Kind is SegmentKind.Bottom or SegmentKind.Descent).Runtime;
}
