using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Represents the machine's side of the service's settings: which repository it serves, how it
/// reaches GitHub, and where its stores live. Read from <c>/etc/remotebuildtool/config.json</c>.
/// </summary>
// Settable rather than init-only: generated deserialization resets an init-only property the file
// leaves out, losing its default.
internal sealed record HostSettings
{
    /// <summary>
    /// Gets or sets the repository the service serves, as <c>owner/name</c>.
    /// </summary>
    public string Repository { get; set; } = "";

    /// <summary>
    /// Gets or sets the GitHub token: read access to the repository's contents and Actions.
    /// </summary>
    public string GitHubToken { get; set; } = "";

    /// <summary>
    /// Gets or sets the GitHub REST API's address.
    /// </summary>
    public string GitHubApiUrl { get; set; } = "https://api.github.com";

    /// <summary>
    /// Gets or sets the address to clone the repository from; GitHub's for <see cref="Repository"/> when empty.
    /// </summary>
    public string? CloneUrl { get; set; }

    /// <summary>
    /// Gets or sets the runner pool the service's builds are recorded under.
    /// </summary>
    public string Pool { get; set; } = "service";

    /// <summary>
    /// Gets or sets the machine the service's builds are recorded under; the host's name when empty.
    /// </summary>
    public string? Machine { get; set; }

    /// <summary>
    /// Gets or sets where the service keeps its checkouts, run stores and state.
    /// </summary>
    public string WorkRoot { get; set; } = "/work";

    /// <summary>
    /// Gets or sets main's artifact store, which the service alone writes.
    /// </summary>
    public string MainStore { get; set; } = "/mnt/main";

    /// <summary>
    /// Gets or sets the staging store: every PR and merge queue run's own artifact store, one folder
    /// per run, kept a day for promotion.
    /// </summary>
    public string StagingRoot { get; set; } = "/mnt/staging";

    /// <summary>
    /// Gets or sets the logs share.
    /// </summary>
    public string LogsRoot { get; set; } = "/mnt/logs";

    /// <summary>
    /// Gets or sets the telemetry share.
    /// </summary>
    public string TelemetryRoot { get; set; } = "/mnt/telemetry";

    /// <summary>
    /// Gets or sets the processes and MSBuild nodes one of the service's builds may run at once; all the
    /// machine's when empty.
    /// </summary>
    public int? MaxCpu { get; set; }

    /// <summary>
    /// Gets the address to clone from.
    /// </summary>
    [JsonIgnore]
    public string EffectiveCloneUrl => CloneUrl is { Length: > 0 } url ? url : $"https://github.com/{Repository}.git";

    /// <summary>
    /// Gets the machine name builds are recorded under.
    /// </summary>
    [JsonIgnore]
    public string EffectiveMachine => Machine is { Length: > 0 } machine ? machine : Environment.MachineName;

    /// <summary>
    /// Reads and checks the host settings.
    /// </summary>
    /// <param name="path">The settings file.</param>
    /// <returns>The settings.</returns>
    /// <exception cref="InvalidOperationException">The file is missing, malformed, or leaves out the repository or the token.</exception>
    public static HostSettings Load(string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"No service settings at {path}. runner/setup-runner.sh writes them.");

        var settings = Deserialize(File.ReadAllText(path), path, ServiceJsonContext.Default.HostSettings);
        if (!settings.Repository.Contains('/', StringComparison.Ordinal))
            throw new InvalidOperationException($"{path}: 'repository' must be owner/name, got '{settings.Repository}'.");
        if (settings.GitHubToken.Length == 0)
            throw new InvalidOperationException($"{path}: 'gitHubToken' is required; the service reads queue and job times from GitHub.");

        return settings;
    }

    internal static T Deserialize<T>(string text, string source, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        try
        {
            return JsonSerializer.Deserialize(text, typeInfo)
                ?? throw new InvalidOperationException($"{source} is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"{source} is not valid: {exception.Message}");
        }
    }
}

/// <summary>
/// Represents the repository's side of the service's settings: what to watch and when to run.
/// Read from <c>remotebuildtool.json</c> at the root of the default branch; every field has a
/// default, so the file is optional.
/// </summary>
internal sealed record RepositorySettings
{
    /// <summary>
    /// The file's name at the repository root.
    /// </summary>
    public const string FileName = "remotebuildtool.json";

    /// <summary>
    /// Gets or sets the branch whose merged pull requests fill main and whose head the daily rebuild builds.
    /// </summary>
    public string Branch { get; set; } = "master";

    /// <summary>
    /// Gets or sets how often to look for pull requests merged into <see cref="Branch"/>, in seconds.
    /// </summary>
    public int PollSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets how often to record the builds GitHub finished, in seconds.
    /// </summary>
    public int BuildReportSeconds { get; set; } = 120;

    /// <summary>
    /// Gets or sets the time of day, in UTC, to compact the telemetry of earlier days and prune old logs.
    /// </summary>
    public TimeOnly Housekeeping { get; set; } = new(3, 30);

    /// <summary>
    /// Gets or sets the time of day, in UTC, to rebuild <see cref="Branch"/> from scratch and switch main
    /// to it, or <see langword="null"/> never to; an empty main is rebuilt whatever the time.
    /// </summary>
    public TimeOnly? Rebuild { get; set; } = new(1, 0);

    /// <summary>
    /// Gets or sets how many days to keep the logs of each kind of build, by the prefix of its folder.
    /// </summary>
    public IReadOnlyDictionary<string, int> LogRetentionDays { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["PR_"] = 30,
        ["Rebuild_"] = 30,
        ["Official_"] = 365,
    };

    /// <summary>
    /// Reads the settings of a checkout.
    /// </summary>
    /// <param name="repositoryRoot">The checkout.</param>
    /// <returns>The settings, or the defaults when the checkout has no settings file.</returns>
    /// <exception cref="InvalidOperationException">The file is malformed or out of range.</exception>
    public static RepositorySettings Load(string repositoryRoot)
    {
        var path = Path.Combine(repositoryRoot, FileName);
        return Parse(File.Exists(path) ? File.ReadAllText(path) : null, path);
    }

    /// <summary>
    /// Reads the settings from the file's text.
    /// </summary>
    /// <param name="text">The file's text, or <see langword="null"/> when there is no file.</param>
    /// <param name="source">Where the text came from, for messages.</param>
    /// <returns>The settings, or the defaults when there is no file.</returns>
    /// <exception cref="InvalidOperationException">The file is malformed or out of range.</exception>
    public static RepositorySettings Parse(string? text, string source)
    {
        if (text is null)
            return new RepositorySettings();

        var settings = HostSettings.Deserialize(text, source, ServiceJsonContext.Default.RepositorySettings);
        if (settings.PollSeconds < 10 || settings.BuildReportSeconds < 10)
            throw new InvalidOperationException($"{source}: 'pollSeconds' and 'buildReportSeconds' must be at least 10.");
        if (settings.Branch.Length == 0)
            throw new InvalidOperationException($"{source}: 'branch' must not be empty.");

        return settings;
    }
}

/// <summary>
/// Provides the serialization metadata for the service's settings and state, generated at compile time.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    WriteIndented = true)]
[JsonSerializable(typeof(HostSettings))]
[JsonSerializable(typeof(RepositorySettings))]
[JsonSerializable(typeof(ServiceState))]
internal sealed partial class ServiceJsonContext : JsonSerializerContext;
