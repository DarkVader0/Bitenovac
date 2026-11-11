using System.Text.Json;

namespace Bitenovac.RemoteBuildTool.Planning;

/// <summary>
/// Represents how many units a stage may build at once.
/// </summary>
/// <param name="Minimum">The fewest.</param>
/// <param name="Maximum">The most.</param>
internal sealed record SlotLimits(int Minimum, int Maximum)
{
    /// <summary>
    /// Gets the limits of a stage that builds one unit at a time, as every stage does unless told otherwise.
    /// </summary>
    public static SlotLimits One { get; } = new(1, 1);
}

/// <summary>
/// Represents the repository's choice of how many units each stage builds at once, read from the
/// <c>sharding</c> section of <c>remotebuildtool.json</c> at the repository root:
/// <code>
/// { "sharding": { "build": { "min": 1, "max": 4 } } }
/// </code>
/// </summary>
/// <param name="Build">The limits of the build stage.</param>
internal sealed record ShardingSettings(SlotLimits Build)
{
    /// <summary>
    /// The file the settings are read from, which also holds the service's.
    /// </summary>
    public const string FileName = "remotebuildtool.json";

    private static readonly JsonDocumentOptions Options = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    /// <summary>
    /// Reads the settings of the repository at <paramref name="repositoryRoot"/>.
    /// </summary>
    /// <param name="repositoryRoot">The repository root.</param>
    /// <returns>The settings; a stage the file does not mention builds one unit at a time.</returns>
    /// <exception cref="InvalidOperationException">The file is malformed, or its limits are out of range.</exception>
    public static ShardingSettings Load(string repositoryRoot)
    {
        var path = Path.Combine(repositoryRoot, FileName);
        if (!File.Exists(path))
            return new ShardingSettings(SlotLimits.One);

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path), Options);
            return new ShardingSettings(Limits(document.RootElement, "build"));
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"{FileName} is not valid: {exception.Message}");
        }
    }

    private static SlotLimits Limits(JsonElement root, string stage)
    {
        if (!root.TryGetProperty("sharding", out var sharding) || !sharding.TryGetProperty(stage, out var limits))
            return SlotLimits.One;

        var minimum = Number(limits, "min", 1, stage);
        var maximum = Number(limits, "max", minimum, stage);
        if (minimum < 1 || maximum < minimum)
            throw new InvalidOperationException($"{FileName}: 'sharding.{stage}' needs 'min' of at least 1 and 'max' of at least 'min'; they are {minimum} and {maximum}.");

        return new SlotLimits(minimum, maximum);
    }

    private static int Number(JsonElement limits, string name, int fallback, string stage)
    {
        if (!limits.TryGetProperty(name, out var value))
            return fallback;

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : throw new InvalidOperationException($"{FileName}: 'sharding.{stage}.{name}' must be a whole number.");
    }
}
