using System.Globalization;
using Bitenovac.DecompressionAlgorithms.Core.Environment;
using Bitenovac.DecompressionAlgorithms.Core.Equipment;
using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Formats values and names for display.
/// </summary>
public static class Display
{
    public static string Number(double value, int decimals = 0) =>
        value.ToString($"F{decimals}", CultureInfo.InvariantCulture);

    public static string Minutes(TimeSpan duration) => $"{Clock(duration)} min";

    public static string Clock(TimeSpan duration)
    {
        var seconds = (int)Math.Round(duration.TotalSeconds, MidpointRounding.AwayFromZero);
        return seconds % 60 == 0 ? $"{seconds / 60}" : $"{seconds / 60}:{seconds % 60:00}";
    }

    public static string Name(CylinderPurpose purpose) => purpose switch
    {
        CylinderPurpose.BottomGas => "Bottom gas",
        CylinderPurpose.DecoGas => "Deco gas",
        CylinderPurpose.Diluent => "Diluent",
        CylinderPurpose.Bailout => "Bailout",
        CylinderPurpose.Oxygen => "Oxygen",
        _ => purpose.ToString()
    };

    public static string Name(CylinderPurpose purpose, CircuitInput circuit) =>
        purpose == CylinderPurpose.Diluent && circuit is SemiClosedCircuitInput ? "Loop supply" : Name(purpose);

    public static string Name(CircuitInput circuit) => circuit switch
    {
        ClosedCircuitInput => "CCR",
        SemiClosedCircuitInput => "SCR",
        _ => "Open circuit"
    };

    public static string Name(SegmentKind kind) => kind switch
    {
        SegmentKind.Descent => "Descent",
        SegmentKind.Bottom => "Level",
        SegmentKind.Ascent => "Ascent",
        SegmentKind.Stop => "Stop",
        SegmentKind.GasSwitch => "Switch",
        _ => kind.ToString()
    };

    public static string Name(Salinity salinity) => salinity switch
    {
        Salinity.Fresh => "Fresh water",
        Salinity.Salt => "Salt water",
        Salinity.Brackish => "Brackish water",
        Salinity.EN13319 => "EN 13319 (dive computer standard)",
        _ => salinity.ToString()
    };

    public static string Name(MaximumOperatingDepthModel model) => model switch
    {
        MaximumOperatingDepthModel.Realistic => "Realistic (salinity and altitude)",
        MaximumOperatingDepthModel.Simplified => "Simplified (10 m per bar)",
        _ => model.ToString()
    };
}
