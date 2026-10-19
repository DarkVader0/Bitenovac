using Bitenovac.DecompressionAlgorithms.Core.Equipment;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Provides the roles a cylinder can have on each breathing apparatus.
/// </summary>
/// <remarks>
/// The planner breathes every cylinder but an oxygen cylinder on open circuit, and feeds a
/// rebreather loop only from its diluent, so a role that does not fit the apparatus would make a
/// cylinder silently unused or wrongly breathed. On a closed-circuit rebreather the oxygen cylinder
/// is pure oxygen, which is always its oxygen cylinder; a cylinder of at least
/// <see cref="MinimumOxygenCylinderPercent"/> may be chosen as one.
/// </remarks>
public static class CylinderRoles
{
    /// <summary>
    /// The oxygen content, in percent, at or above which a closed-circuit rebreather's cylinder may be its oxygen cylinder.
    /// </summary>
    public const double MinimumOxygenCylinderPercent = 95;

    private const double PureOxygenPercent = 100;

    private static readonly CylinderPurpose[] OpenCircuitRoles = [CylinderPurpose.BottomGas, CylinderPurpose.DecoGas];
    private static readonly CylinderPurpose[] RebreatherRoles = [CylinderPurpose.Diluent, CylinderPurpose.Bailout];
    private static readonly CylinderPurpose[] ClosedCircuitRolesWithOxygen = [CylinderPurpose.Diluent, CylinderPurpose.Bailout, CylinderPurpose.Oxygen];

    /// <summary>
    /// Returns the roles the user can choose between for a cylinder on the specified apparatus.
    /// </summary>
    /// <param name="circuit">The breathing apparatus of the dive.</param>
    /// <param name="oxygenPercent">The oxygen content of the cylinder, in percent.</param>
    /// <returns>
    /// The selectable roles. A closed-circuit rebreather offers oxygen only for a cylinder of at least
    /// <see cref="MinimumOxygenCylinderPercent"/>, and an unknown apparatus offers none.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="circuit"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<CylinderPurpose> Selectable(CircuitInput circuit, double oxygenPercent)
    {
        ArgumentNullException.ThrowIfNull(circuit);

        return circuit switch
        {
            OpenCircuitInput => OpenCircuitRoles,
            ClosedCircuitInput when oxygenPercent >= MinimumOxygenCylinderPercent => ClosedCircuitRolesWithOxygen,
            ClosedCircuitInput or SemiClosedCircuitInput => RebreatherRoles,
            _ => []
        };
    }

    /// <summary>
    /// Returns whether a cylinder's role is fixed by its content rather than chosen.
    /// </summary>
    /// <param name="circuit">The breathing apparatus of the dive.</param>
    /// <param name="cylinder">The cylinder.</param>
    /// <returns>
    /// <see langword="true"/> for pure oxygen on a closed-circuit rebreather, which is always its oxygen cylinder;
    /// otherwise <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="circuit"/> or <paramref name="cylinder"/> is <see langword="null"/>.</exception>
    public static bool IsFixed(CircuitInput circuit, CylinderInput cylinder)
    {
        ArgumentNullException.ThrowIfNull(circuit);
        ArgumentNullException.ThrowIfNull(cylinder);

        return circuit is ClosedCircuitInput && cylinder.OxygenPercent >= PureOxygenPercent;
    }

    /// <summary>
    /// Returns the role a cylinder has on the specified apparatus.
    /// </summary>
    /// <param name="circuit">The breathing apparatus of the dive.</param>
    /// <param name="cylinder">The cylinder.</param>
    /// <returns>
    /// <see cref="CylinderPurpose.Oxygen"/> for a fixed cylinder (see <see cref="IsFixed"/>), the
    /// cylinder's own role when the apparatus allows it, and otherwise the matching role on the
    /// apparatus: a bottom gas becomes the diluent and a deco gas a bailout, and back.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="circuit"/> or <paramref name="cylinder"/> is <see langword="null"/>.</exception>
    public static CylinderPurpose Resolve(CircuitInput circuit, CylinderInput cylinder)
    {
        ArgumentNullException.ThrowIfNull(circuit);
        ArgumentNullException.ThrowIfNull(cylinder);

        if (IsFixed(circuit, cylinder))
        {
            return CylinderPurpose.Oxygen;
        }

        var selectable = Selectable(circuit, cylinder.OxygenPercent);
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
