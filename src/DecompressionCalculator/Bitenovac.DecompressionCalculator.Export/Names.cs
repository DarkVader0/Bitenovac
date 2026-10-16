using Bitenovac.DecompressionAlgorithms.Core.Environment;
using Bitenovac.DecompressionAlgorithms.Core.Equipment;
using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Export;

/// <summary>
/// Provides the names shown for the planner's values in the app and in exported plans.
/// </summary>
public static class Names
{
    /// <summary>
    /// Returns the name of a cylinder role, such as <c>Deco gas</c>.
    /// </summary>
    /// <param name="purpose">The role.</param>
    /// <returns>The name, or the enum member name when the role is not known.</returns>
    public static string Of(CylinderPurpose purpose) => purpose switch
    {
        CylinderPurpose.BottomGas => ExportText.PurposeBottomGas,
        CylinderPurpose.DecoGas => ExportText.PurposeDecoGas,
        CylinderPurpose.Diluent => ExportText.PurposeDiluent,
        CylinderPurpose.Bailout => ExportText.PurposeBailout,
        CylinderPurpose.Oxygen => ExportText.PurposeOxygen,
        _ => purpose.ToString()
    };

    /// <summary>
    /// Returns the short name of a breathing apparatus, such as <c>CCR</c>.
    /// </summary>
    /// <param name="circuit">The apparatus.</param>
    /// <returns><c>CCR</c>, <c>SCR</c>, or <c>Open circuit</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="circuit"/> is <see langword="null"/>.</exception>
    public static string Of(CircuitInput circuit)
    {
        ArgumentNullException.ThrowIfNull(circuit);

        return circuit switch
        {
            ClosedCircuitInput => ExportText.CircuitClosed,
            SemiClosedCircuitInput => ExportText.CircuitSemiClosed,
            _ => ExportText.CircuitOpen
        };
    }

    /// <summary>
    /// Returns the name of a phase of a dive, such as <c>Stop</c>.
    /// </summary>
    /// <param name="kind">The phase.</param>
    /// <returns>The name; a bottom segment is a <c>Level</c>.</returns>
    public static string Of(SegmentKind kind) => kind switch
    {
        SegmentKind.Descent => ExportText.SegmentDescent,
        SegmentKind.Bottom => ExportText.SegmentLevel,
        SegmentKind.Ascent => ExportText.SegmentAscent,
        SegmentKind.Stop => ExportText.SegmentStop,
        SegmentKind.GasSwitch => ExportText.SegmentSwitch,
        SegmentKind.SafetyStop => ExportText.SegmentSafetyStop,
        _ => kind.ToString()
    };

    /// <summary>
    /// Returns the name of a type of water, such as <c>Salt water</c>.
    /// </summary>
    /// <param name="salinity">The type of water.</param>
    /// <returns>The name, or the enum member name when the type is not known.</returns>
    public static string Of(Salinity salinity) => salinity switch
    {
        Salinity.Fresh => ExportText.SalinityFresh,
        Salinity.Salt => ExportText.SalinitySalt,
        Salinity.Brackish => ExportText.SalinityBrackish,
        Salinity.EN13319 => ExportText.SalinityEn13319,
        _ => salinity.ToString()
    };
}
