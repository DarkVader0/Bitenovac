using System.Text.Json.Serialization;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Represents the chosen decompression algorithm and its own settings.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "algorithm")]
[JsonDerivedType(typeof(BuhlmannZhl16cSettings), BuhlmannZhl16cSettings.AlgorithmId)]
public abstract record AlgorithmSettings;

/// <summary>
/// Represents the settings of the Bühlmann ZHL-16C algorithm with gradient factors.
/// </summary>
/// <param name="GradientFactorLow">The gradient factor at the first stop, in percent, greater than zero and at most <paramref name="GradientFactorHigh"/>.</param>
/// <param name="GradientFactorHigh">The gradient factor at the surface, in percent, at most 100.</param>
public sealed record BuhlmannZhl16cSettings(double GradientFactorLow, double GradientFactorHigh) : AlgorithmSettings
{
    /// <summary>
    /// The identifier stored with a plan that uses this algorithm.
    /// </summary>
    public const string AlgorithmId = "buhlmann-zhl16c";
}
