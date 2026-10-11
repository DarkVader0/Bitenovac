namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Specifies the units a plan's values are entered and shown in.
/// </summary>
/// <remarks>
/// Oxygen partial pressures and setpoints are in bar, and loop volume and metabolic oxygen
/// consumption in liters, in both systems.
/// </remarks>
public enum UnitSystem
{
    /// <summary>
    /// Meters, bar, and liters: cylinder sizes are water volumes.
    /// </summary>
    Metric,

    /// <summary>
    /// Feet, psi, and cubic feet: cylinder sizes are rated gas volumes at a rated pressure.
    /// </summary>
    Imperial
}
