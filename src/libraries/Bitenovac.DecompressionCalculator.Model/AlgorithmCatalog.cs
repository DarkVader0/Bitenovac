using Bitenovac.DecompressionAlgorithms.BuhlmannZhl16c;
using Bitenovac.DecompressionAlgorithms.Core.Abstractions;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Represents one algorithm the user can pick.
/// </summary>
/// <param name="Id">The identifier stored with a plan.</param>
/// <param name="DisplayName">The name shown to the user.</param>
/// <param name="DefaultSettings">The settings a newly picked algorithm starts with.</param>
public sealed record AlgorithmDescriptor(string Id, string DisplayName, AlgorithmSettings DefaultSettings);

/// <summary>
/// Provides the decompression algorithms a plan can use.
/// </summary>
public static class AlgorithmCatalog
{
    /// <summary>
    /// Gets every algorithm, in the order they are offered.
    /// </summary>
    public static IReadOnlyList<AlgorithmDescriptor> Algorithms { get; } =
    [
        new(BuhlmannZhl16cSettings.AlgorithmId, "Bühlmann ZHL-16C with gradient factors", new BuhlmannZhl16cSettings(30, 70))
    ];

    /// <summary>
    /// Creates a fresh algorithm for one plan.
    /// </summary>
    /// <remarks>
    /// An algorithm holds the state of the dive it plans, so every plan needs its own instance.
    /// </remarks>
    internal static IDecompressionAlgorithm Create(AlgorithmSettings settings) => settings switch
    {
        BuhlmannZhl16cSettings buhlmann => new BuhlmannZhl16cAlgorithm(
            buhlmann.GradientFactorLow / 100.0,
            buhlmann.GradientFactorHigh / 100.0),
        _ => throw new ArgumentOutOfRangeException(nameof(settings), settings.GetType().Name, "The algorithm is not in the catalog.")
    };
}
