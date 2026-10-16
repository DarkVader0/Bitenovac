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
        var units = plan.Units == UnitSystem.Imperial ? ExportText.Imperial : ExportText.Metric;
        return Localized.Format(ExportText.Header, Algorithm(plan), units, Date(exportedAt));
    }

    public static string Algorithm(DivePlanDocument plan) => plan.Algorithm switch
    {
        BuhlmannZhl16cSettings buhlmann =>
            Localized.Format(ExportText.Buhlmann, Formats.Number(buhlmann.GradientFactorLow), Formats.Number(buhlmann.GradientFactorHigh)),
        _ => ExportText.UnknownAlgorithm
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

        return Localized.Format(ExportText.SettingsLine,
            Names.Of(settings.Salinity), Formats.Number(settings.Altitude), labels.Depth,
            Formats.Number(settings.BottomSac, gasRateDecimals), Formats.Number(settings.DecoSac, gasRateDecimals), labels.GasRate,
            Formats.Number(settings.BottomPo2, 1), Formats.Number(settings.DecoPo2, 1),
            lastStop, settings.SafetyStop ? ExportText.On : ExportText.Off,
            settings.OxygenNarcotic ? ExportText.Narcotic : ExportText.NotNarcotic);
    }

    public static string Inputs(DiveInput dive, UnitSystem units)
    {
        var labels = UnitLabels.For(units);
        var levels = string.Join(ExportText.ListSeparator, dive.Levels.Select(level =>
            Localized.Format(ExportText.InputLevel, Formats.Number(level.Depth), labels.Depth, Formats.Number(level.DurationMinutes))));
        var cylinders = string.Join(ExportText.ListSeparator, dive.Cylinders.Select(cylinder =>
            Localized.Format(ExportText.InputCylinder, Gas(cylinder), Formats.Number(cylinder.Size, 1), labels.CylinderSize,
                Formats.Number(cylinder.StartPressure), labels.Pressure, Names.Of(cylinder.Purpose))));
        return Localized.Format(ExportText.InputsLine, Names.Of(dive.Circuit), levels, cylinders);
    }

    public static string ContingencyName(Contingency contingency, UnitLabels labels) => contingency.Kind == ContingencyKind.LostGas
        ? Localized.Format(ExportText.ContingencyLost, contingency.LostGas)
        : Localized.Format(ExportText.ContingencyExtended, Formats.Number(contingency.ExtraMinutes), Formats.Number(contingency.ExtraDepth), labels.Depth);

    public static string Describe(ValidationIssue issue) => IssueText.DescribeWithDive(issue);

    public static string Gas(CylinderInput cylinder) =>
        GasMixture.FromPercent(cylinder.OxygenPercent, cylinder.HeliumPercent).ToString();
}
