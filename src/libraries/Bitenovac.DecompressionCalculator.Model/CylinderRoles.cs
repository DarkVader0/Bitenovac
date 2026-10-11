using Bitenovac.DecompressionAlgorithms.Core.Equipment;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Provides the roles a cylinder can have on each breathing apparatus.
/// </summary>
/// <remarks>
/// The planner breathes every cylinder but an oxygen cylinder on open circuit, and feeds a
/// rebreather loop only from its diluent, so a role that does not fit the apparatus would make a
/// cylinder silently unused or wrongly breathed. The oxygen cylinder of a closed-circuit
/// rebreather is never chosen: it is the cylinder holding pure oxygen.
/// </remarks>
public static class CylinderRoles
{
    private const double PureOxygenPercent = 100;

    private static readonly CylinderPurpose[] OpenCircuitRoles = [CylinderPurpose.BottomGas, CylinderPurpose.DecoGas];
    private static readonly CylinderPurpose[] RebreatherRoles = [CylinderPurpose.Diluent, CylinderPurpose.Bailout];

    /// <summary>
    /// Returns the roles the user can choose between for a cylinder on the specified apparatus.
    /// </summary>
    /// <param name="circuit">The breathing apparatus of the dive.</param>
    /// <returns>The selectable roles, or none for an apparatus this library does not know.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="circuit"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<CylinderPurpose> Selectable(CircuitInput circuit)
    {
        ArgumentNullException.ThrowIfNull(circuit);

        return circuit switch
        {
            OpenCircuitInput => OpenCircuitRoles,
            ClosedCircuitInput or SemiClosedCircuitInput => RebreatherRoles,
            _ => []
        };
    }

    /// <summary>
    /// Returns the role a cylinder has on the specified apparatus.
    /// </summary>
    /// <param name="circuit">The breathing apparatus of the dive.</param>
    /// <param name="cylinder">The cylinder.</param>
    /// <returns>
    /// <see cref="CylinderPurpose.Oxygen"/> for pure oxygen on a closed-circuit rebreather, the
    /// cylinder's own role when the apparatus allows it, and otherwise the matching role on the
    /// apparatus: a bottom gas becomes the diluent and a deco gas a bailout, and back.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="circuit"/> or <paramref name="cylinder"/> is <see langword="null"/>.</exception>
    public static CylinderPurpose Resolve(CircuitInput circuit, CylinderInput cylinder)
    {
        ArgumentNullException.ThrowIfNull(circuit);
        ArgumentNullException.ThrowIfNull(cylinder);

        if (circuit is ClosedCircuitInput && cylinder.OxygenPercent >= PureOxygenPercent)
        {
            return CylinderPurpose.Oxygen;
        }

        var selectable = Selectable(circuit);
        if (selectable.Count == 0 || selectable.Contains(cylinder.Purpose))
        {
            return cylinder.Purpose;
        }

        return circuit switch
        {
            OpenCircuitInput => cylinder.Purpose == CylinderPurpose.Diluent ? CylinderPurpose.BottomGas : CylinderPurpose.DecoGas,
            _ => cylinder.Purpose == CylinderPurpose.BottomGas ? CylinderPurpose.Diluent : CylinderPurpose.Bailout
        };
    }

    /// <summary>
    /// Returns the dive with every cylinder's role resolved for its apparatus.
    /// </summary>
    /// <param name="dive">The dive.</param>
    /// <returns>The dive, with each cylinder given the role <see cref="Resolve"/> returns.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dive"/> is <see langword="null"/>.</exception>
    public static DiveInput Apply(DiveInput dive)
    {
        ArgumentNullException.ThrowIfNull(dive);

        return dive with
        {
            Cylinders = [.. dive.Cylinders.Select(cylinder => cylinder with { Purpose = Resolve(dive.Circuit, cylinder) })]
        };
    }
}
