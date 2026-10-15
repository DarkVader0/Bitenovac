using System.Globalization;
using Bitenovac.DecompressionAlgorithms.Core.Environment;
using Bitenovac.DecompressionAlgorithms.Core.Equipment;
using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionCalculator.Export;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Formats values and names for display; the ones an exported plan also shows come from the Export library.
/// </summary>
public static class Display
{
    public static string Number(double value, int decimals = 0) => Formats.Number(value, decimals);

    public static string Minutes(TimeSpan duration) => Formats.Minutes(duration);

    public static string Clock(TimeSpan duration) => Formats.Clock(duration);

    public static string HoursAndMinutes(TimeSpan duration) => Formats.HoursAndMinutes(duration);

    public static string Name(CylinderPurpose purpose) => Names.Of(purpose);

    public static string Name(CylinderPurpose purpose, CircuitInput circuit) =>
        purpose == CylinderPurpose.Diluent && circuit is SemiClosedCircuitInput ? "Loop supply" : Names.Of(purpose);

    public static string Name(CircuitInput circuit) => Names.Of(circuit);

    public static string Name(SegmentKind kind) => Names.Of(kind);

    public static string Name(Salinity salinity) => Names.Of(salinity);

    public static string Name(MaximumOperatingDepthModel model) => model switch
    {
        MaximumOperatingDepthModel.Realistic => "Realistic (salinity and altitude)",
        MaximumOperatingDepthModel.Simplified => "Simplified (10 m per bar)",
        _ => model.ToString()
    };
}
