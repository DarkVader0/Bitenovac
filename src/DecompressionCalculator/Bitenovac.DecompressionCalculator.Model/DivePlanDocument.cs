using Bitenovac.DecompressionAlgorithms.Core.Equipment;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Represents a saved plan: one or more dives in a row, the algorithm, and the settings they share.
/// </summary>
public sealed record DivePlanDocument
{
    private readonly DiveInput[] _dives = [];
    private readonly DateTime _createdAt;
    private readonly DateTime _modifiedAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="DivePlanDocument"/> class.
    /// </summary>
    /// <param name="id">The identifier of the plan.</param>
    /// <param name="name">The name the user gave the plan.</param>
    /// <param name="createdAt">When the plan was first saved, in UTC.</param>
    /// <param name="modifiedAt">When the plan was last changed, in UTC.</param>
    /// <param name="units">The unit system every value is in.</param>
    /// <param name="algorithm">The decompression algorithm and its settings.</param>
    /// <param name="settings">The planner settings shared by every dive.</param>
    /// <param name="dives">The dives, in the order they are made.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/>, <paramref name="algorithm"/>, <paramref name="settings"/>, or <paramref name="dives"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="createdAt"/> or <paramref name="modifiedAt"/> is not in UTC.</exception>
    public DivePlanDocument(
        Guid id,
        string name,
        DateTime createdAt,
        DateTime modifiedAt,
        UnitSystem units,
        AlgorithmSettings algorithm,
        PlannerSettings settings,
        IReadOnlyList<DiveInput> dives)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(algorithm);
        ArgumentNullException.ThrowIfNull(settings);

        Id = id;
        Name = name;
        CreatedAt = createdAt;
        ModifiedAt = modifiedAt;
        Units = units;
        Algorithm = algorithm;
        Settings = settings;
        Dives = dives;
    }

    /// <summary>
    /// Gets the identifier of the plan.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the name the user gave the plan.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets when the plan was first saved, in UTC.
    /// </summary>
    /// <exception cref="ArgumentException">The value set is not in UTC.</exception>
    public DateTime CreatedAt
    {
        get => _createdAt;
        init => _createdAt = RequireUtc(value);
    }

    /// <summary>
    /// Gets when the plan was last changed, in UTC.
    /// </summary>
    /// <exception cref="ArgumentException">The value set is not in UTC.</exception>
    public DateTime ModifiedAt
    {
        get => _modifiedAt;
        init => _modifiedAt = RequireUtc(value);
    }

    /// <summary>
    /// Gets the unit system every value is in.
    /// </summary>
    public UnitSystem Units { get; init; }

    /// <summary>
    /// Gets the decompression algorithm and its settings.
    /// </summary>
    public AlgorithmSettings Algorithm { get; init; }

    /// <summary>
    /// Gets the planner settings shared by every dive.
    /// </summary>
    public PlannerSettings Settings { get; init; }

    /// <summary>
    /// Gets the dives, in the order they are made.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value set is <see langword="null"/>.</exception>
    public IReadOnlyList<DiveInput> Dives
    {
        get => _dives;
        init => _dives = [.. value ?? throw new ArgumentNullException(nameof(value))];
    }

    /// <summary>
    /// Creates a plan with one simple dive, the first algorithm in the catalog, and default settings.
    /// </summary>
    /// <param name="id">The identifier of the plan.</param>
    /// <param name="name">The name of the plan.</param>
    /// <param name="units">The unit system the plan is entered in.</param>
    /// <param name="now">The current time in UTC, recorded as both creation and modification time.</param>
    /// <returns>A plan for a single open-circuit air dive to 30 m (100 ft) for 20 minutes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="now"/> is not in UTC.</exception>
    public static DivePlanDocument CreateNew(Guid id, string name, UnitSystem units, DateTime now)
    {
        var dive = units switch
        {
            UnitSystem.Imperial => new DiveInput(0, new OpenCircuitInput(), [new LevelInput(100, 20)],
                [new CylinderInput(21, 0, 80, 3000, 3000, CylinderPurpose.BottomGas)]),
            _ => new DiveInput(0, new OpenCircuitInput(), [new LevelInput(30, 20)],
                [new CylinderInput(21, 0, 12, 0, 200, CylinderPurpose.BottomGas)])
        };

        return new DivePlanDocument(id, name, now, now, units, AlgorithmCatalog.Algorithms[0].DefaultSettings,
            PlannerSettings.Default(units), [dive]);
    }

    /// <inheritdoc/>
    public bool Equals(DivePlanDocument? other) =>
        other is not null
        && Id.Equals(other.Id)
        && Name == other.Name
        && CreatedAt.Equals(other.CreatedAt)
        && ModifiedAt.Equals(other.ModifiedAt)
        && Units == other.Units
        && Algorithm.Equals(other.Algorithm)
        && Settings.Equals(other.Settings)
        && _dives.AsSpan().SequenceEqual(other._dives);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Id, Name, ModifiedAt, _dives.Length);

    private static DateTime RequireUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : throw new ArgumentException("The time must be in UTC.", nameof(value));
}
