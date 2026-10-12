using System.Globalization;
using Bitenovac.DecompressionAlgorithms.Core.Calculations;
using Bitenovac.DecompressionAlgorithms.Core.Planning;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Provides a method for describing why a dive could not be planned, in the plan's units.
/// </summary>
internal static class FailureMessages
{
    public static string Describe(InvalidOperationException exception, UnitSystem units) => exception switch
    {
        InsufficientGasException gas => string.Create(CultureInfo.InvariantCulture,
            $"The demand for gas exceeds what the matching cylinder holds: the profile draws " +
            $"{UnitConversion.FromGasVolume(gas.Demand, units):0.##} {GasVolumeUnit(units)} of {gas.Gas} from a cylinder holding " +
            $"{UnitConversion.FromGasVolume(gas.Available, units):0.##} {GasVolumeUnit(units)}."),
        StopNotClearedException stop => string.Create(CultureInfo.InvariantCulture,
            $"The decompression stop at {UnitConversion.FromDepth(stop.StopDepth, units):0.#} {DepthUnit(units)} does not clear within " +
            $"{stop.Limit.TotalHours:0.#} hours; the gradient factors and available gases cannot decompress this dive."),
        _ => exception.Message,
    };

    private static string GasVolumeUnit(UnitSystem units) => units == UnitSystem.Imperial ? "cu ft" : "L";

    private static string DepthUnit(UnitSystem units) => units == UnitSystem.Imperial ? "ft" : "m";
}
