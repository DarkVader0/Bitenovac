using System.Globalization;
using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionCalculator.Export.Pdf;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Export;

/// <summary>
/// Provides a method for exporting a plan as a printable A4 PDF dive sheet.
/// </summary>
public static class DiveSheet
{
    /// <summary>
    /// The note printed at the foot of every page.
    /// </summary>
    public const string Disclaimer = "Bitenovac - for planning only. Follow your dive computer and your training.";

    private const double ChartHeight = 190;
    private const double DiveOpeningHeight = 110 + ChartHeight;
    private static readonly PdfColor StopColor = PdfColor.FromRgb(201, 138, 0);
    private static readonly PdfColor SwitchColor = PdfColor.FromRgb(28, 128, 67);
    private static readonly PdfColor Water = PdfColor.FromRgb(238, 245, 252);
    private static readonly PdfColor DiveFill = PdfColor.FromRgb(214, 230, 245);

    /// <summary>
    /// Returns the plan as the bytes of a PDF file.
    /// </summary>
    /// <param name="plan">The plan, which is calculated for the sheet.</param>
    /// <param name="exportedAt">When the sheet is made, in UTC; printed on the sheet and stored as its creation time.</param>
    /// <returns>The PDF file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="plan"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// A plan with issues is exported with the issues listed instead of its results.
    /// </remarks>
    public static byte[] Create(DivePlanDocument plan, DateTime exportedAt)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var calculation = PlanCalculator.Calculate(plan);
        var labels = UnitLabels.For(plan.Units);
        var sheet = new SheetWriter();
        var title = string.IsNullOrWhiteSpace(plan.Name) ? "Dive plan" : plan.Name;

        sheet.Paragraph(title, PdfFont.HelveticaBold, 18, SheetWriter.Text);
        sheet.Paragraph(Summary.Header(plan, exportedAt), PdfFont.Helvetica, 9, SheetWriter.Muted);
        sheet.Paragraph(Summary.Settings(plan), PdfFont.Helvetica, 9, SheetWriter.Muted);

        if (!calculation.IsCalculated)
        {
            sheet.Advance(10);
            sheet.Paragraph("This plan cannot be calculated:", PdfFont.HelveticaBold, 11, SheetWriter.Danger);
            foreach (var issue in calculation.Issues)
            {
                sheet.Paragraph($"- {Summary.Describe(issue)}", PdfFont.Helvetica, 10, SheetWriter.Text);
            }
        }
        else
        {
            for (var index = 0; index < plan.Dives.Count; index++)
            {
                WriteDive(sheet, plan, index, calculation.Dives[index], labels);
            }
        }

        return sheet.Build(title, exportedAt, Disclaimer);
    }

    private static void WriteDive(SheetWriter sheet, DivePlanDocument plan, int index, DiveResult result, UnitLabels labels)
    {
        var dive = plan.Dives[index];

        sheet.Advance(12);
        sheet.EnsureSpace(DiveOpeningHeight);
        var heading = plan.Dives.Count == 1 ? "Dive" : $"Dive {index + 1}";
        if (index > 0)
        {
            heading += $" - after {Formats.Number(dive.SurfaceIntervalMinutes)} min on the surface";
        }

        sheet.Paragraph(heading, PdfFont.HelveticaBold, 13, SheetWriter.Text);
        sheet.Paragraph(Summary.Inputs(dive, plan.Units), PdfFont.Helvetica, 9.5, SheetWriter.Text);

        if (result.FailureMessage is { } failure)
        {
            sheet.Paragraph(failure, PdfFont.HelveticaBold, 10, SheetWriter.Danger);
            return;
        }

        sheet.Advance(6);
        WriteFigures(sheet, result, labels);
        WriteWarnings(sheet, result, labels);

        sheet.EnsureSpace(ChartHeight);
        WriteChart(sheet, ProfileChart.Create(result, plan.Units), labels);

        var showsApparatus = result.Profile.Any(static phase => phase.Circuit != BreathingLoop.OpenCircuit.ToString());
        SheetColumn[] profileColumns =
        [
            new("", 70), new($"Depth ({labels.Depth})", 90, PdfAlign.Right), new("Time (min)", 70, PdfAlign.Right),
            new("Run (min)", 70, PdfAlign.Right), new("Gas", 80), new(showsApparatus ? "Apparatus" : "", 135)
        ];
        var profileRows = result.Profile
            .Select(phase => new[]
            {
                Names.Of(phase.Kind), Depths(phase), Formats.Clock(phase.Duration), Formats.Clock(phase.Runtime), phase.Gas,
                showsApparatus ? phase.Circuit : string.Empty
            })
            .ToArray();
        sheet.Table(profileColumns, profileRows,
            row => result.Profile[row].Kind is SegmentKind.Stop or SegmentKind.GasSwitch);

        sheet.Advance(10);
        SheetColumn[] gasColumns =
        [
            new("Gas", 65), new("Used as", 85), new($"Used ({labels.GasVolume})", 85, PdfAlign.Right),
            new($"Pressure ({labels.Pressure})", 110, PdfAlign.Right), new($"Reserve ({labels.GasVolume})", 85, PdfAlign.Right),
            new($"Left ({labels.GasVolume})", 85, PdfAlign.Right)
        ];
        var gasRows = result.Cylinders
            .Select(static cylinder => new[]
            {
                cylinder.Gas, Names.Of(cylinder.Purpose), Formats.Number(cylinder.GasUsed),
                $"{Formats.Number(cylinder.StartPressure)} - {Formats.Number(cylinder.EndPressure)}",
                Formats.Number(cylinder.RequiredReserve), Formats.Number(cylinder.ProjectedRemaining)
            })
            .ToArray();
        sheet.Table(gasColumns, gasRows, _ => false);
    }

    private static void WriteFigures(SheetWriter sheet, DiveResult result, UnitLabels labels)
    {
        (string Label, string Value)[] figures =
        [
            ("Runtime", Formats.Minutes(result.TotalRuntime)),
            ("Stop time", Formats.Minutes(result.StopTime)),
            ("Maximum depth", $"{Formats.Number(result.MaximumDepth)} {labels.Depth}"),
            ("CNS", $"{Formats.Number(result.CnsPercent)} %"),
            ("OTU", Formats.Number(result.OxygenToleranceUnits))
        ];

        const double gap = 6;
        const double height = 36;
        var width = (SheetWriter.ContentWidth - gap * (figures.Length - 1)) / figures.Length;
        sheet.EnsureSpace(height + 8);

        for (var index = 0; index < figures.Length; index++)
        {
            var left = SheetWriter.Margin + index * (width + gap);
            sheet.Page.Rectangle(left, sheet.Y, width, height, SheetWriter.Sunken);
            sheet.Page.Text(left + 7, sheet.Y + 13, figures[index].Label, PdfFont.Helvetica, 7.5, SheetWriter.Muted);
            sheet.Page.Text(left + 7, sheet.Y + 29, figures[index].Value, PdfFont.HelveticaBold, 12, SheetWriter.Text);
        }

        sheet.Advance(height + 8);
    }

    private static void WriteWarnings(SheetWriter sheet, DiveResult result, UnitLabels labels)
    {
        foreach (var violation in result.Violations)
        {
            sheet.Paragraph(
                $"Ascent from {Formats.Number(violation.FromDepth)} to {Formats.Number(violation.ToDepth)} {labels.Depth} " +
                $"passes the decompression ceiling at {Formats.Number(violation.Ceiling, 1)} {labels.Depth}.",
                PdfFont.HelveticaBold, 9.5, SheetWriter.Danger);
        }

        foreach (var cylinder in result.Cylinders.Where(static cylinder => cylinder.Shortfall > 0))
        {
            sheet.Paragraph(
                $"{cylinder.Gas} runs out: the dive needs {Formats.Number(cylinder.Shortfall)} {labels.GasVolume} more than the cylinder holds.",
                PdfFont.HelveticaBold, 9.5, SheetWriter.Danger);
        }

        if (!result.ReserveSatisfied)
        {
            sheet.Paragraph("A cylinder does not hold the reserve gas the plan requires.", PdfFont.HelveticaBold, 9.5, SheetWriter.Danger);
        }
    }

    private static void WriteChart(SheetWriter sheet, ProfileChart chart, UnitLabels labels)
    {
        var page = sheet.Page;
        var area = new ChartArea(SheetWriter.Margin + 30, sheet.Y + 14, SheetWriter.ContentWidth - 34, ChartHeight - 40);

        page.Rectangle(area.Left, area.Top, area.Width, area.Height, Water);
        foreach (var depth in chart.DepthTicks)
        {
            var y = chart.Y(depth, area);
            page.Line(area.Left, y, area.Left + area.Width, y, 0.4, SheetWriter.Rule);
            page.Text(area.Left - 5, y + 3, Formats.Number(depth), PdfFont.Helvetica, 7.5, SheetWriter.Muted, PdfAlign.Right);
        }

        foreach (var minutes in chart.TimeTicks)
        {
            var x = chart.X(minutes, area);
            page.Line(x, area.Top, x, area.Top + area.Height, 0.4, SheetWriter.Rule);
            page.Text(x, area.Top + area.Height + 11, Formats.Number(minutes), PdfFont.Helvetica, 7.5, SheetWriter.Muted, PdfAlign.Center);
        }

        var points = chart.Points.Select(point => (chart.X(point.Minutes, area), chart.Y(point.Depth, area))).ToArray();
        page.Polygon(points, DiveFill);
        page.Polyline(points, 1.6, SheetWriter.Brand);

        if (chart.Ceiling.Count > 0)
        {
            page.Polyline([.. chart.Ceiling.Select(point => (chart.X(point.Minutes, area), chart.Y(point.Depth, area)))], 1.2, SheetWriter.Danger);
        }

        foreach (var stop in chart.Stops)
        {
            var y = chart.Y(stop.Depth, area);
            page.Line(chart.X(stop.StartMinutes, area), y, chart.X(stop.EndMinutes, area), y, 3.2,
                stop.IsGasSwitch ? SwitchColor : StopColor);
            if (stop.IsGasSwitch)
            {
                page.Text(chart.X(stop.StartMinutes, area) + 3, y - 5, stop.Gas, PdfFont.HelveticaBold, 7.5, SwitchColor);
            }
        }

        page.Text(area.Left + 3, area.Top - 4, labels.Depth, PdfFont.Helvetica, 7.5, SheetWriter.Muted);
        page.Text(area.Left + area.Width, area.Top + area.Height + 22, "min", PdfFont.Helvetica, 7.5, SheetWriter.Muted, PdfAlign.Right);
        sheet.Advance(ChartHeight);
    }

    private static string Depths(ProfileRow phase) => phase.StartDepth.Equals(phase.EndDepth)
        ? Formats.Number(phase.EndDepth)
        : string.Create(CultureInfo.InvariantCulture, $"{Formats.Number(phase.StartDepth)} - {Formats.Number(phase.EndDepth)}");
}
