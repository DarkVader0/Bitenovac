using System.Globalization;
using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Export;

/// <summary>
/// Provides the one-line descriptions of a plan shared by the dive sheet and the text summary.
/// </summary>
internal static class Summary
{
    public static string Header(DivePlanDocument plan, DateTime exportedAt)
    {
        var units = plan.Units == UnitSystem.Imperial ? "Imperial" : "Metric";
        return $"{Algorithm(plan)} · {units} · exported {Date(exportedAt)} UTC";
    }

    public static string Algorithm(DivePlanDocument plan) => plan.Algorithm switch
    {
        BuhlmannZhl16cSettings buhlmann =>
            $"Bühlmann ZHL-16C GF {Formats.Number(buhlmann.GradientFactorLow)}/{Formats.Number(buhlmann.GradientFactorHigh)}",
        _ => "Unknown algorithm"
    };

    public static string Date(DateTime exportedAt) => exportedAt.ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture);

    public static string Settings(DivePlanDocument plan) => Settings(plan, plan.Settings);

    public static string Settings(DivePlanDocument plan, PlannerSettings settings)
    {
        var labels = UnitLabels.For(plan.Units);
        var lastStop = settings.LastStopAtSixMeters
            ? plan.Units == UnitSystem.Imperial ? "20 ft" : "6 m"
            : plan.Units == UnitSystem.Imperial ? "10 ft" : "3 m";
        var gasRateDecimals = plan.Units == UnitSystem.Imperial ? 2 : 0;

        return $"{Names.Of(settings.Salinity)} · altitude {Formats.Number(settings.Altitude)} {labels.Depth} · " +
               $"SAC {Formats.Number(settings.BottomSac, gasRateDecimals)} / {Formats.Number(settings.DecoSac, gasRateDecimals)} {labels.GasRate} · " +
               $"PO2 {Formats.Number(settings.BottomPo2, 1)} / {Formats.Number(settings.DecoPo2, 1)} bar · " +
               $"last stop {lastStop} · safety stop {(settings.SafetyStop ? "on" : "off")} · " +
               $"O2 {(settings.OxygenNarcotic ? "narcotic" : "not narcotic")} in END";
    }

    public static string Inputs(DiveInput dive, UnitSystem units)
    {
        var labels = UnitLabels.For(units);
        var levels = string.Join(", ", dive.Levels.Select(level =>
            $"{Formats.Number(level.Depth)} {labels.Depth} {Formats.Number(level.DurationMinutes)} min"));
        var cylinders = string.Join(", ", dive.Cylinders.Select(cylinder =>
            $"{Gas(cylinder)} {Formats.Number(cylinder.Size, 1)} {labels.CylinderSize} at {Formats.Number(cylinder.StartPressure)} {labels.Pressure} ({Names.Of(cylinder.Purpose)})"));
        return $"{Names.Of(dive.Circuit)} · {levels} · {cylinders}";
    }

    public static string Describe(ValidationIssue issue) => IssueText.DescribeWithDive(issue);

    public static string Gas(CylinderInput cylinder) =>
        GasMixture.FromPercent(cylinder.OxygenPercent, cylinder.HeliumPercent).ToString();
}
