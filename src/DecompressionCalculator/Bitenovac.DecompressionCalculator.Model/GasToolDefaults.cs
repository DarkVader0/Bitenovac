using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Represents the values the gas calculator starts with, for a plan's settings and units.
/// </summary>
/// <param name="ModPo2">The oxygen partial pressure limit of the maximum operating depth, in bar.</param>
/// <param name="MixPo2">The oxygen partial pressure limit of the best mix, in bar.</param>
/// <param name="EndDepth">The depth of the equivalent narcotic depth, in meters or feet.</param>
/// <param name="MixDepth">The deepest depth of the best mix, in meters or feet.</param>
/// <param name="MixEnd">The narcotic limit of the best mix, in meters or feet.</param>
public sealed record GasToolDefaults(double ModPo2, double MixPo2, double EndDepth, double MixDepth, double MixEnd)
{
    /// <summary>
    /// Returns the starting values of the gas calculator: the plan's oxygen limits, and depths that suit its units.
    /// </summary>
    /// <param name="settings">The planner settings whose oxygen limits the calculator starts with.</param>
    /// <param name="units">The units the depths are shown in.</param>
    /// <returns>The starting values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    public static GasToolDefaults For(PlannerSettings settings, UnitSystem units)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return units switch
        {
            UnitSystem.Imperial => new GasToolDefaults(settings.DecoPo2, settings.BottomPo2, 150, 200, 100),
            _ => new GasToolDefaults(settings.DecoPo2, settings.BottomPo2, 45, 60, 30)
        };
    }
}
