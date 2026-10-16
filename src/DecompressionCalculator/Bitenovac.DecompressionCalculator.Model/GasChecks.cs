using Bitenovac.DecompressionAlgorithms.Core.Calculations;
using Bitenovac.DecompressionAlgorithms.Core.Planning;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Represents a gas as it is breathed at one depth of a dive: on a level, or at the deepest depth it is used.
/// </summary>
/// <param name="Gas">The gas, such as <c>TX18/45</c>; the diluent on a rebreather.</param>
/// <param name="Depth">The depth, in meters or feet.</param>
/// <param name="OxygenPressure">The partial pressure of oxygen breathed, in bar.</param>
/// <param name="EquivalentNarcoticDepth">The equivalent narcotic depth, in meters or feet.</param>
/// <param name="Density">The density of the gas breathed, in grams per liter.</param>
public sealed record GasCheckRow(string Gas, double Depth, double OxygenPressure, double EquivalentNarcoticDepth, double Density)
{
    /// <summary>
    /// Gets a value indicating whether the gas is denser than <see cref="GasMath.DensityLimit"/>.
    /// </summary>
    public bool IsTooDense => Density > GasMath.DensityLimit;
}

/// <summary>
/// Represents a gas switch that risks isobaric counterdiffusion: from a helium-rich gas to one with much more nitrogen.
/// </summary>
/// <param name="From">The gas switched from.</param>
/// <param name="To">The gas switched to.</param>
/// <param name="Depth">The depth of the switch, in meters or feet.</param>
public sealed record CounterdiffusionRow(string From, string To, double Depth);

internal static class GasChecks
{
    /// <remarks>
    /// A switch is flagged by the rule of fifths: the nitrogen fraction may rise by at most a fifth of what the helium
    /// fraction falls.
    /// </remarks>
    public static (GasCheckRow[] Rows, CounterdiffusionRow[] Switches) For(
        IReadOnlyList<DiveSegment> segments, DivePlanSettings environment, bool oxygenNarcotic, UnitSystem units)
    {
        var indexed = segments.Select(static (segment, index) => (Segment: segment, Index: index)).ToArray();
        var deepest = indexed
            .GroupBy(static entry => entry.Segment.Gas)
            .Select(static group => group.MaxBy(static entry => entry.Segment.Depth.InMeter));
        var rows = indexed
            .Where(static entry => entry.Segment.Kind == SegmentKind.Bottom)
            .Concat(deepest)
            .DistinctBy(static entry => (entry.Segment.Gas, entry.Segment.Loop, entry.Segment.Depth.InMeter))
            .OrderBy(static entry => entry.Index)
            .Select(entry => Row(entry.Segment, environment, oxygenNarcotic, units))
            .ToArray();

        var switches = new List<CounterdiffusionRow>();
        for (var index = 1; index < segments.Count; index++)
        {
            var before = segments[index - 1];
            var after = segments[index];
            if (before.Gas == after.Gas)
            {
                continue;
            }

            var ambient = AmbientConditions.PressureAtDepth(environment, before.Depth);
            var heliumDrop = before.Loop.InspiredHeliumFraction(before.Gas, ambient) - after.Loop.InspiredHeliumFraction(after.Gas, ambient);
            var nitrogenRise = after.Loop.InspiredNitrogenFraction(after.Gas, ambient) - before.Loop.InspiredNitrogenFraction(before.Gas, ambient);
            if (heliumDrop > 0 && nitrogenRise > heliumDrop / 5)
            {
                switches.Add(new CounterdiffusionRow(before.Gas.ToString(), after.Gas.ToString(), UnitConversion.FromDepth(before.Depth, units)));
            }
        }

        return (rows, [.. switches]);
    }

    private static GasCheckRow Row(DiveSegment segment, DivePlanSettings environment, bool oxygenNarcotic, UnitSystem units)
    {
        var ambient = AmbientConditions.PressureAtDepth(environment, segment.Depth);
        var oxygen = segment.Loop.InspiredOxygenFraction(segment.Gas, ambient);
        var nitrogen = segment.Loop.InspiredNitrogenFraction(segment.Gas, ambient);
        var helium = segment.Loop.InspiredHeliumFraction(segment.Gas, ambient);
        return new GasCheckRow(
            segment.Gas.ToString(),
            UnitConversion.FromDepth(segment.Depth, units),
            oxygen * ambient.InBar,
            GasMath.EquivalentNarcoticDepth(oxygen, nitrogen, ambient, environment, oxygenNarcotic, units),
            GasMath.Density(oxygen, nitrogen, helium, ambient));
    }
}
