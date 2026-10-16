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

        text.AppendLine(string.IsNullOrWhiteSpace(plan.Name) ? "Dive plan" : plan.Name);
        text.AppendLine(Summary.Header(plan, exportedAt));
        text.AppendLine(Summary.Settings(plan));

        if (!calculation.IsCalculated)
        {
            text.AppendLine();
            text.AppendLine("This plan cannot be calculated:");
            foreach (var issue in calculation.Issues)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"- {Summary.Describe(issue)}");
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

        text.AppendLine();
        text.Append(plan.Dives.Count == 1 && string.IsNullOrWhiteSpace(dive.Name) ? "Dive" : dive.DisplayName(index + 1));
        if (index > 0)
        {
            text.Append(CultureInfo.InvariantCulture, $" (after {Formats.HoursAndMinutes(TimeSpan.FromMinutes(dive.SurfaceIntervalMinutes))} h on the surface)");
        }

        text.AppendLine(CultureInfo.InvariantCulture, $": {Summary.Inputs(dive, plan.Units)}");
        if (dive.Settings is { } settings)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"Settings: {Summary.Settings(plan, settings)}");
        }

        if (!result.Succeeded)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"Cannot be planned: {result.FailureMessage}");
            return;
        }

        var stops = result.Profile
            .Where(static phase => phase.Kind == SegmentKind.Stop)
            .Select(phase => Stop(phase, labels))
            .ToArray();
        var safetyStop = result.Profile.FirstOrDefault(static phase => phase.Kind == SegmentKind.SafetyStop);

        text.Append(stops.Length > 0
            ? $"Stops: {string.Join(", ", stops)}"
            : safetyStop is null ? "No stops" : "No decompression stops");
        if (safetyStop is not null)
        {
            text.Append(CultureInfo.InvariantCulture, $" · safety stop {Stop(safetyStop, labels)}");
        }

        text.AppendLine();

        text.AppendLine(CultureInfo.InvariantCulture,
            $"Runtime {Formats.Minutes(result.TotalRuntime)} · max {Formats.Number(result.MaximumDepth)} {labels.Depth} · " +
            $"CNS {Formats.Number(result.CnsPercent)} % · OTU {Formats.Number(result.OxygenToleranceUnits)}");

        foreach (var cylinder in result.Cylinders)
        {
            text.AppendLine(
                $"{cylinder.Gas}: {Formats.Number(cylinder.GasUsed)} {labels.GasVolume} used, " +
                $"{Formats.Number(cylinder.StartPressure)} to {Formats.Number(cylinder.EndPressure)} {labels.Pressure}" +
                (cylinder.Shortfall > 0 ? $" - runs out, {Formats.Number(cylinder.Shortfall)} {labels.GasVolume} short" : string.Empty) +
                (cylinder.ReserveSatisfied ? string.Empty : " - reserve not met"));
        }

        if (!result.IsValid)
        {
            text.AppendLine("Warning: an ascent between levels passes the decompression ceiling.");
        }

        foreach (var warning in GasWarnings.For(result, plan.Units))
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"Warning: {warning}");
        }

        text.AppendLine("Contingencies:");
        foreach (var contingency in result.Contingencies)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"- {Contingency(contingency, labels)}");
        }
    }

    private static string Contingency(Contingency contingency, UnitLabels labels)
    {
        var name = contingency.Kind == ContingencyKind.LostGas
            ? $"{contingency.LostGas} lost"
            : $"+{Formats.Number(contingency.ExtraMinutes)} min, +{Formats.Number(contingency.ExtraDepth)} {labels.Depth}";
        var result = contingency.Result;
        if (!result.Succeeded)
        {
            return $"{name}: cannot be planned: {result.FailureMessage}";
        }

        var stops = result.Profile
            .Where(static phase => phase.Kind == SegmentKind.Stop)
            .Select(phase => Stop(phase, labels))
            .ToArray();
        return $"{name}: runtime {Formats.Minutes(result.TotalRuntime)} · " +
               (stops.Length > 0 ? $"stops {string.Join(", ", stops)}" : "no decompression stops") +
               $" · CNS {Formats.Number(result.CnsPercent)} %";
    }

    private static string Stop(ProfileRow stop, UnitLabels labels) =>
        $"{Formats.Number(stop.EndDepth)} {labels.Depth} {Formats.Clock(stop.Duration)} min";
}
