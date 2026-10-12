using System.Globalization;
using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionAlgorithms.Core.Calculations;

/// <summary>
/// The exception that is thrown when a profile draws more gas from a cylinder than it holds.
/// </summary>
/// <remarks>
/// The message states the volumes in liters. A caller that shows them in other units reads
/// <see cref="Demand"/> and <see cref="Available"/> instead.
/// </remarks>
public sealed class InsufficientGasException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InsufficientGasException"/> class.
    /// </summary>
    /// <param name="gas">The gas in the cylinder.</param>
    /// <param name="demand">The free-gas volume the profile draws, at surface conditions.</param>
    /// <param name="available">The free-gas volume the cylinder holds, at surface conditions.</param>
    public InsufficientGasException(GasMixture gas, Volume demand, Volume available)
        : base(string.Create(CultureInfo.InvariantCulture,
            $"The demand for gas exceeds what the matching cylinder holds: the profile draws " +
            $"{demand.InLiter:0.##} L of {gas} from a cylinder holding {available.InLiter:0.##} L."))
    {
        Gas = gas;
        Demand = demand;
        Available = available;
    }

    /// <summary>
    /// Gets the gas in the cylinder.
    /// </summary>
    public GasMixture Gas { get; }

    /// <summary>
    /// Gets the free-gas volume the profile draws, at surface conditions.
    /// </summary>
    public Volume Demand { get; }

    /// <summary>
    /// Gets the free-gas volume the cylinder holds, at surface conditions.
    /// </summary>
    public Volume Available { get; }
}
