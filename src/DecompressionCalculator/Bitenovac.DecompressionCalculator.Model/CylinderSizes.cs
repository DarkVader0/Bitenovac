namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Changes a cylinder's size, working pressure and fill pressure the way a diver names a tank.
/// </summary>
/// <remarks>
/// An imperial cylinder is named by the gas it holds at its working pressure ("80 cubic feet at 3000 psi"), so its
/// water volume is that size scaled by one atmosphere over the working pressure. The water volume is what the
/// cylinder is: changing the working pressure keeps it, and changing the fill pressure changes only the gas in it.
/// A metric cylinder's size is its water volume already, and it has no working pressure.
/// </remarks>
public static class CylinderSizes
{
    private const int SizeDecimals = 1;

    /// <summary>
    /// Returns the cylinder with a new size, in liters of water or cubic feet of gas at its working pressure.
    /// </summary>
    /// <param name="cylinder">The cylinder.</param>
    /// <param name="size">The new size.</param>
    /// <returns>The changed cylinder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cylinder"/> is <see langword="null"/>.</exception>
    public static CylinderInput WithSize(CylinderInput cylinder, double size)
    {
        ArgumentNullException.ThrowIfNull(cylinder);
        return cylinder with { Size = size };
    }

    /// <summary>
    /// Returns the cylinder filled to a new pressure. The cylinder itself does not change.
    /// </summary>
    /// <param name="cylinder">The cylinder.</param>
    /// <param name="pressure">The fill pressure, in bar or psi.</param>
    /// <returns>The changed cylinder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cylinder"/> is <see langword="null"/>.</exception>
    public static CylinderInput WithStartPressure(CylinderInput cylinder, double pressure)
    {
        ArgumentNullException.ThrowIfNull(cylinder);
        return cylinder with { StartPressure = pressure };
    }

    /// <summary>
    /// Returns an imperial cylinder with a new working pressure, keeping its water volume so its size in cubic feet
    /// follows the pressure.
    /// </summary>
    /// <param name="cylinder">The imperial cylinder.</param>
    /// <param name="pressure">The new working pressure, in psi.</param>
    /// <returns>The changed cylinder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cylinder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The size is rounded to one decimal, as a size converted from metric is. A working pressure that is not positive is
    /// kept as given, so the plan reports it as invalid.
    /// </remarks>
    public static CylinderInput WithWorkingPressure(CylinderInput cylinder, double pressure)
    {
        ArgumentNullException.ThrowIfNull(cylinder);
        return cylinder.RatedPressure > 0 && pressure > 0
            ? cylinder with { Size = Math.Round(cylinder.Size * pressure / cylinder.RatedPressure, SizeDecimals), RatedPressure = pressure }
            : cylinder with { RatedPressure = pressure };
    }
}
