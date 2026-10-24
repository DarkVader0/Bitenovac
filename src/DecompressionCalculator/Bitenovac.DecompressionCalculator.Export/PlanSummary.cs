using System.Globalization;
using System.Text;
using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Export;

/// <summary>
/// Provides a method for describing a plan as short text, to send as a message.
/// </summary>
public static class PlanSummary
{
    /// <summary>
    /// Returns the plan and its results as a few lines of plain text.
    /// </summary>
    /// <param name="plan">The plan, which is calculated for the summary.</param>
    /// <param name="exportedAt">When the summary is made, in UTC.</param>
    /// <returns>
    /// The plan name, the algorithm and settings, then for every dive its levels, cylinders, stops,
    /// runtime, oxygen exposure, and gas used; or the issues when the plan cannot be calculated.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="plan"/> is <see langword="null"/>.</exception>
    public static string Text(DivePlanDocument plan, DateTime exportedAt)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var calculation = PlanCalculator.Calculate(plan);
        var labels = UnitLabels.For(plan.Units);
        var text = new StringBuilder();

        text.AppendLine(string.IsNullOrWhiteSpace(plan.Name) ? ExportText.DivePlan : plan.Name);
        text.AppendLine(Summary.Header(plan, exportedAt));
        text.AppendLine(Summary.Settings(plan));

        if (!calculation.IsCalculated)
        {
            text.AppendLine();
            text.AppendLine(ExportText.CannotBeCalculated);
            foreach (var issue in calculation.Issues)
            {
                text.AppendLine(Localized.Format(ExportText.IssueLine, Summary.Describe(issue)));
            }
        }
        else
        {
            for (var index = 0; index < plan.Dives.Count; index++)
            {
                AppendDive(text, plan, index, calculation.Dives[index], labels);
            }
        }

        text.AppendLine();
        text.Append(DiveSheet.Disclaimer);
        return text.ToString();
    }

    private static void AppendDive(StringBuilder text, DivePlanDocument plan, int index, DiveResult result, UnitLabels labels)
    {
        var dive = plan.Dives[index];
        var quantity = plan.SettingsFor(index).GasQuantity;

        text.AppendLine();
        text.Append(plan.Dives.Count == 1 && string.IsNullOrWhiteSpace(dive.Name) ? ExportText.Dive : dive.DisplayName(index + 1));
        if (index > 0)
        {
            text.Append(Localized.Format(ExportText.SummaryInterval, Formats.HoursAndMinutes(TimeSpan.FromMinutes(dive.SurfaceIntervalMinutes))));
        }

        text.AppendLine(Localized.Format(ExportText.SummaryInputs, Summary.Inputs(dive, plan.Units)));
        if (dive.Settings is { } settings)
        {
            text.AppendLine(Localized.Format(ExportText.SummarySettings, Summary.Settings(plan, settings)));
        }

        if (!result.Succeeded)
        {
            text.AppendLine(Localized.Format(ExportText.SummaryCannotBePlanned, result.FailureMessage));
            return;
        }

        var stops = result.Profile
            .Where(static phase => phase.Kind == SegmentKind.Stop)
            .Select(phase => Stop(phase, labels))
            .ToArray();
        var safetyStop = result.Profile.FirstOrDefault(static phase => phase.Kind == SegmentKind.SafetyStop);

        text.Append(stops.Length > 0
            ? Localized.Format(ExportText.SummaryStops, string.Join(ExportText.ListSeparator, stops))
            : safetyStop is null ? ExportText.SummaryNoStops : ExportText.SummaryNoDecoStops);
        if (safetyStop is not null)
        {
            text.Append(Localized.Format(ExportText.SummarySafetyStop, Stop(safetyStop, labels)));
        }

        text.AppendLine();

        text.AppendLine(Localized.Format(ExportText.SummaryFigures, Formats.Minutes(result.TotalRuntime), Formats.Number(result.MaximumDepth),
            labels.Depth, Formats.Number(result.CnsPercent), Formats.Number(result.OxygenToleranceUnits)));

        foreach (var cylinder in result.Cylinders)
        {
            var amount = GasAmounts.For(cylinder, quantity, plan.Units);
            var unit = quantity == GasQuantity.Pressure ? labels.Pressure : labels.GasVolume;
            text.AppendLine(
                Localized.Format(ExportText.SummaryCylinder, cylinder.Gas, Formats.Number(amount.Used), unit,
                    Formats.Number(amount.Start), Formats.Number(amount.End), unit) +
                (cylinder.MissingLitres > 0 ? Localized.Format(ExportText.SummaryRunsOut, Formats.Number(amount.Missing), unit) : string.Empty) +
                (cylinder.ReserveSatisfied ? string.Empty : ExportText.SummaryReserveNotMet));
        }

        foreach (var warning in GasWarnings.For(result, plan.Units))
        {
            text.AppendLine(Localized.Format(ExportText.SummaryWarning, warning));
        }

        text.AppendLine(ExportText.SummaryContingencies);
        foreach (var contingency in result.Contingencies)
        {
            text.AppendLine(Localized.Format(ExportText.IssueLine, Contingency(contingency, labels)));
        }
    }

    private static string Contingency(Contingency contingency, UnitLabels labels)
    {
        var name = Summary.ContingencyName(contingency, labels);
        var result = contingency.Result;
        if (!result.Succeeded)
        {
            return Localized.Format(ExportText.SummaryContingencyFailed, name, result.FailureMessage);
        }

        var stops = result.Profile
            .Where(static phase => phase.Kind == SegmentKind.Stop)
            .Select(phase => Stop(phase, labels))
            .ToArray();
        return Localized.Format(ExportText.SummaryContingency, name, Formats.Minutes(result.TotalRuntime),
            stops.Length > 0
                ? Localized.Format(ExportText.SummaryContingencyStops, string.Join(ExportText.ListSeparator, stops))
                : ExportText.SummaryContingencyNoStops,
            Formats.Number(result.CnsPercent));
    }

    private static string Stop(ProfileRow stop, UnitLabels labels) =>
        Localized.Format(ExportText.SummaryStop, Formats.Number(stop.EndDepth), labels.Depth, Formats.Clock(stop.Duration));
}
