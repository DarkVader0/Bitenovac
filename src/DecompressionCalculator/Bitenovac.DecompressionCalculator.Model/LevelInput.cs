namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Represents one level of a dive, as entered.
/// </summary>
/// <param name="Depth">The depth of the level, in meters or feet.</param>
/// <param name="DurationMinutes">The time spent at the depth, in minutes. Descent and travel between levels are added on top.</param>
public sealed record LevelInput(double Depth, double DurationMinutes);
