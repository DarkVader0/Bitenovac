using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Identifies a block of a dive's results, which the diver can show, hide and put in order.
/// </summary>
public enum ResultBlock
{
    /// <summary>
    /// Runtime, time to surface, CNS and oxygen tolerance units.
    /// </summary>
    Figures,

    /// <summary>
    /// The phases of the dive: depth, time, runtime and gas.
    /// </summary>
    Profile,

    /// <summary>
    /// The gas each cylinder uses, and what is left.
    /// </summary>
    Gas,

    /// <summary>
    /// The depth-over-time chart.
    /// </summary>
    Chart,

    /// <summary>
    /// The dive planned again for what could go wrong.
    /// </summary>
    Contingencies
}

/// <summary>
/// Represents whether one result block is shown.
/// </summary>
/// <param name="Block">The block.</param>
/// <param name="Visible">Whether the block is shown.</param>
public sealed record ResultBlockSetting(ResultBlock Block, bool Visible);

/// <summary>
/// Specifies how the app is coloured.
/// </summary>
public enum ThemePreference
{
    /// <summary>
    /// Light surfaces and dark text; the default.
    /// </summary>
    Light,

    /// <summary>
    /// Dark surfaces and light text.
    /// </summary>
    Dark,

    /// <summary>
    /// Light or dark as the device is set.
    /// </summary>
    System
}

/// <summary>
/// Represents what the diver has chosen for the app itself, as opposed to for a plan: the theme, the language,
/// and which result blocks are shown in which order.
/// </summary>
/// <remarks>
/// The document is versioned so a later version of the app can read what an earlier one stored.
/// </remarks>
public sealed record UserPreferences
{
    /// <summary>
    /// The version of the document this code writes.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// The language the app starts in.
    /// </summary>
    public const string DefaultLanguage = "en";

    private static readonly ResultBlock[] DefaultOrder =
        [ResultBlock.Figures, ResultBlock.Profile, ResultBlock.Gas, ResultBlock.Chart, ResultBlock.Contingencies];

    private readonly ResultBlockSetting[] _blocks;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserPreferences"/> class; everything not given is the default.
    /// </summary>
    /// <param name="version">The version of the document.</param>
    /// <param name="theme">How the app is coloured.</param>
    /// <param name="language">The language tag of the app; the default when empty or <see langword="null"/>.</param>
    /// <param name="blocks">The result blocks as stored, in order; see <see cref="Blocks"/>.</param>
    [JsonConstructor]
    public UserPreferences(
        int version = CurrentVersion,
        ThemePreference theme = ThemePreference.Light,
        string? language = null,
        IReadOnlyList<ResultBlockSetting>? blocks = null)
    {
        Version = version;
        Theme = theme;
        Language = string.IsNullOrWhiteSpace(language) ? DefaultLanguage : language;
        _blocks = Normalize(blocks ?? []);
    }

    /// <summary>
    /// Gets the version of the document, as written.
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// Gets how the app is coloured.
    /// </summary>
    public ThemePreference Theme { get; init; }

    /// <summary>
    /// Gets the language of the app, as a language tag such as <c>en</c>.
    /// </summary>
    public string Language { get; init; }

    /// <summary>
    /// Gets every result block in the order it is shown, each with whether it is shown.
    /// </summary>
    /// <remarks>
    /// Blocks that are not valid or are repeated are dropped, and blocks a stored document does not know about are
    /// added at the end, shown.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value set is <see langword="null"/>.</exception>
    public IReadOnlyList<ResultBlockSetting> Blocks
    {
        get => _blocks;
        init => _blocks = Normalize(value ?? throw new ArgumentNullException(nameof(value)));
    }

    /// <summary>
    /// Gets the preferences a diver starts with.
    /// </summary>
    public static UserPreferences Default { get; } = new();

    /// <summary>
    /// Returns the blocks that are shown, in the order they are shown.
    /// </summary>
    /// <returns>The shown blocks.</returns>
    public IReadOnlyList<ResultBlock> VisibleBlocks() => [.. _blocks.Where(static setting => setting.Visible).Select(static setting => setting.Block)];

    /// <summary>
    /// Returns the preferences with a block shown or hidden.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <param name="visible">Whether the block is shown.</param>
    /// <returns>The changed preferences.</returns>
    public UserPreferences WithVisible(ResultBlock block, bool visible) =>
        this with { Blocks = [.. _blocks.Select(setting => setting.Block == block ? setting with { Visible = visible } : setting)] };

    /// <summary>
    /// Returns the preferences with a block moved up or down the order.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <param name="offset">How many places to move it: negative towards the first, positive towards the last.</param>
    /// <returns>The changed preferences; the same ones when the block is already at the end it is moved towards.</returns>
    public UserPreferences Move(ResultBlock block, int offset)
    {
        var index = Array.FindIndex(_blocks, setting => setting.Block == block);
        var target = Math.Clamp(index + offset, 0, _blocks.Length - 1);
        if (index < 0 || target == index)
        {
            return this;
        }

        var moved = _blocks.ToList();
        var setting = moved[index];
        moved.RemoveAt(index);
        moved.Insert(target, setting);
        return this with { Blocks = moved };
    }

    /// <inheritdoc/>
    public bool Equals(UserPreferences? other) =>
        other is not null
        && Version == other.Version
        && Theme == other.Theme
        && string.Equals(Language, other.Language, StringComparison.Ordinal)
        && _blocks.AsSpan().SequenceEqual(other._blocks);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Version, Theme, Language, _blocks.Length);

    private static ResultBlockSetting[] Normalize(IReadOnlyList<ResultBlockSetting> blocks)
    {
        var kept = blocks
            .Where(static setting => Enum.IsDefined(setting.Block))
            .DistinctBy(static setting => setting.Block)
            .ToList();

        kept.AddRange(DefaultOrder.Where(block => kept.All(setting => setting.Block != block)).Select(static block => new ResultBlockSetting(block, true)));
        return [.. kept];
    }
}

/// <summary>
/// Provides methods for writing and reading <see cref="UserPreferences"/> as JSON.
/// </summary>
public static class UserPreferencesJson
{
    /// <summary>
    /// Writes preferences as JSON.
    /// </summary>
    /// <param name="preferences">The preferences.</param>
    /// <returns>The JSON.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="preferences"/> is <see langword="null"/>.</exception>
    public static string Serialize(UserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        return JsonSerializer.Serialize(preferences, UserPreferencesJsonContext.Default.UserPreferences);
    }

    /// <summary>
    /// Reads preferences from JSON, or gives the defaults when the JSON is missing or is not a stored document.
    /// </summary>
    /// <param name="json">The stored JSON, or <see langword="null"/> when nothing is stored.</param>
    /// <returns>The preferences, with the current version.</returns>
    public static UserPreferences Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return UserPreferences.Default;
        }

        try
        {
            return (JsonSerializer.Deserialize(json, UserPreferencesJsonContext.Default.UserPreferences) ?? UserPreferences.Default)
                with { Version = UserPreferences.CurrentVersion };
        }
        catch (JsonException)
        {
            return UserPreferences.Default;
        }
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(UserPreferences))]
internal sealed partial class UserPreferencesJsonContext : JsonSerializerContext;
