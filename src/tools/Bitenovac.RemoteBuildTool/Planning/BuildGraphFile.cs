using System.Text.Json;
using System.Text.Json.Serialization;
using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Planning;

/// <summary>
/// Represents one project of a build graph: what it is, what it references, and how its
/// fingerprint came about.
/// </summary>
/// <param name="Project">The project, repository-relative.</param>
/// <param name="Toolchain">The toolchain that builds it.</param>
/// <param name="IsTestProject">Whether it is a test project.</param>
/// <param name="References">The projects it references, repository-relative.</param>
/// <param name="OwnHash">The fingerprint of its own inputs.</param>
/// <param name="FullHash">The fingerprint of its own inputs and its references'.</param>
/// <param name="CacheOutcome"><c>hit</c> when main held it, <c>miss</c> otherwise.</param>
/// <param name="WhyCacheMiss">Why it missed, if it did.</param>
/// <param name="OwnHashInputs">Every input of its own fingerprint, as <c>key=digest</c>.</param>
internal sealed record BuildGraphNode(
    string Project,
    string Toolchain,
    bool IsTestProject,
    IReadOnlyList<string> References,
    string OwnHash,
    string FullHash,
    string CacheOutcome,
    string? WhyCacheMiss,
    IReadOnlyList<string> OwnHashInputs);

/// <summary>
/// Represents a build's project graph, as kept beside its logs or in its drop so builds can be
/// compared later: which projects there were, how they depended on each other, and what each
/// one's fingerprint was made of.
/// </summary>
/// <param name="SchemaVersion">The version of this format.</param>
/// <param name="BuildId">The build.</param>
/// <param name="Queue">The build's queue.</param>
/// <param name="Sha">The commit built.</param>
/// <param name="FingerprintFormat">The fingerprint rules the hashes follow; hashes of different rules do not compare.</param>
/// <param name="Configurations">The projects of each configuration, ordered by path.</param>
internal sealed record BuildGraphFile(
    int SchemaVersion,
    string BuildId,
    string Queue,
    string? Sha,
    int FingerprintFormat,
    IReadOnlyDictionary<string, IReadOnlyList<BuildGraphNode>> Configurations)
{
    /// <summary>
    /// The file's name, beside a build's logs and in an Official drop.
    /// </summary>
    public const string FileName = "build-graph.json";

    /// <summary>
    /// Writes the graph of a plan.
    /// </summary>
    /// <param name="directory">The directory to write <see cref="FileName"/> into.</param>
    /// <param name="identity">The build.</param>
    /// <param name="byConfiguration">The plan's entries by configuration.</param>
    /// <returns>The path written.</returns>
    public static string Write(string directory, BuildIdentity identity, IReadOnlyDictionary<string, List<PlanEntry>> byConfiguration)
    {
        var graph = new BuildGraphFile(
            1,
            identity.BuildId,
            identity.Queue,
            identity.Sha,
            Hashing.FingerprintFormat.Version,
            byConfiguration.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<BuildGraphNode>)[.. pair.Value
                    .OrderBy(entry => entry.ProjectPath, StringComparer.Ordinal)
                    .Select(entry => new BuildGraphNode(
                        entry.ProjectPath,
                        entry.Toolchain,
                        entry.IsTestProject,
                        [.. entry.References.Order(StringComparer.Ordinal)],
                        entry.OwnHash,
                        entry.FullHash,
                        entry.Hit ? "hit" : "miss",
                        entry.WhyCacheMiss,
                        entry.OwnHashInputs))],
                StringComparer.Ordinal));

        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, FileName);
        File.WriteAllText(path, JsonSerializer.Serialize(graph, BuildGraphJsonContext.Default.BuildGraphFile));
        return path;
    }

    /// <summary>
    /// Reads a graph written by <see cref="Write"/>.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>The graph.</returns>
    /// <exception cref="InvalidOperationException">The file is not a build graph.</exception>
    public static BuildGraphFile Read(string path) =>
        JsonSerializer.Deserialize(File.ReadAllText(path), BuildGraphJsonContext.Default.BuildGraphFile)
            ?? throw new InvalidOperationException($"{path} is not a build graph.");
}

/// <summary>
/// Provides the serialization metadata for <see cref="BuildGraphFile"/>, generated at compile time.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(BuildGraphFile))]
internal sealed partial class BuildGraphJsonContext : JsonSerializerContext;
