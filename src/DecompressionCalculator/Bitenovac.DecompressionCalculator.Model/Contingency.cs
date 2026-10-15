namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Represents what a contingency plan assumes went wrong.
/// </summary>
public enum ContingencyKind
{
    /// <summary>
    /// The deepest level was deeper and longer than planned.
    /// </summary>
    Extended,

    /// <summary>
    /// A decompression gas was lost.
    /// </summary>
    LostGas
}

/// <summary>
/// Represents a dive planned again for something that went wrong, next to the plan it belongs to.
/// </summary>
/// <param name="Kind">What went wrong.</param>
/// <param name="Result">The dive planned for it; it holds no timeline and no contingencies of its own.</param>
public sealed record Contingency(ContingencyKind Kind, DiveResult Result)
{
    /// <summary>
    /// Gets the time added to the deepest level, in minutes, for an <see cref="ContingencyKind.Extended"/> contingency.
    /// </summary>
    public double ExtraMinutes { get; init; }

    /// <summary>
    /// Gets the depth added to the deepest level, in meters or feet, for an <see cref="ContingencyKind.Extended"/> contingency.
    /// </summary>
    public double ExtraDepth { get; init; }

    /// <summary>
    /// Gets the gas that was lost, such as <c>EAN50</c>, for a <see cref="ContingencyKind.LostGas"/> contingency.
    /// </summary>
    public string? LostGas { get; init; }
}
