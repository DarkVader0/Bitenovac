namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Represents one dive of a plan, as entered.
/// </summary>
public sealed record DiveInput
{
    private readonly LevelInput[] _levels = [];
    private readonly CylinderInput[] _cylinders = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="DiveInput"/> class.
    /// </summary>
    /// <param name="surfaceIntervalMinutes">The time on the surface before this dive, in minutes. Ignored for the first dive of a plan.</param>
    /// <param name="circuit">The breathing apparatus.</param>
    /// <param name="levels">The levels, in the order they are dived.</param>
    /// <param name="cylinders">The cylinders carried.</param>
    /// <exception cref="ArgumentNullException"><paramref name="circuit"/>, <paramref name="levels"/>, or <paramref name="cylinders"/> is <see langword="null"/>.</exception>
    public DiveInput(
        double surfaceIntervalMinutes,
        CircuitInput circuit,
        IReadOnlyList<LevelInput> levels,
        IReadOnlyList<CylinderInput> cylinders)
    {
        ArgumentNullException.ThrowIfNull(circuit);

        SurfaceIntervalMinutes = surfaceIntervalMinutes;
        Circuit = circuit;
        Levels = levels;
        Cylinders = cylinders;
    }

    /// <summary>
    /// Gets the time on the surface before this dive, in minutes. Ignored for the first dive of a plan.
    /// </summary>
    public double SurfaceIntervalMinutes { get; init; }

    /// <summary>
    /// Gets the breathing apparatus.
    /// </summary>
    public CircuitInput Circuit { get; init; }

    /// <summary>
    /// Gets the levels, in the order they are dived.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value set is <see langword="null"/>.</exception>
    public IReadOnlyList<LevelInput> Levels
    {
        get => _levels;
        init => _levels = [.. value ?? throw new ArgumentNullException(nameof(value))];
    }

    /// <summary>
    /// Gets the cylinders carried.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value set is <see langword="null"/>.</exception>
    public IReadOnlyList<CylinderInput> Cylinders
    {
        get => _cylinders;
        init => _cylinders = [.. value ?? throw new ArgumentNullException(nameof(value))];
    }

    /// <inheritdoc/>
    public bool Equals(DiveInput? other) =>
        other is not null
        && SurfaceIntervalMinutes.Equals(other.SurfaceIntervalMinutes)
        && Circuit.Equals(other.Circuit)
        && _levels.AsSpan().SequenceEqual(other._levels)
        && _cylinders.AsSpan().SequenceEqual(other._cylinders);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(SurfaceIntervalMinutes, Circuit, _levels.Length, _cylinders.Length);
}
