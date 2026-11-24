using System.Text.Json.Serialization;

namespace Bitenovac.RemoteDeployTool;

/// <summary>
/// Represents one artifact deployed, or not, to one target: a row of the ledger.
/// </summary>
internal sealed record DeploymentInfo
{
    /// <summary>
    /// The result of a deployment that went through.
    /// </summary>
    public const string Succeeded = "succeeded";

    /// <summary>
    /// The result of a deployment that did not.
    /// </summary>
    public const string Failed = "failed";

    /// <summary>
    /// Gets the version of the ledger's shape the row was written in.
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Gets where the artifact went, as in <c>nuget:https://api.nuget.org/v3/index.json</c> or <c>github-pages</c>.
    /// </summary>
    public required string Target { get; init; }

    /// <summary>
    /// Gets the artifact's kind, as in <see cref="DropArtifact.Kind"/>.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Gets the artifact's name, as in <see cref="DropArtifact.Name"/>.
    /// </summary>
    public required string Artifact { get; init; }

    /// <summary>
    /// Gets the artifact's version, for a package.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Gets the drop the artifact came from.
    /// </summary>
    public required string Drop { get; init; }

    /// <summary>
    /// Gets the commit the drop was built from, when known.
    /// </summary>
    public string? Sha { get; init; }

    /// <summary>
    /// Gets the fingerprint of the project behind the artifact, when known.
    /// </summary>
    public string? Fingerprint { get; init; }

    /// <summary>
    /// Gets <see cref="Succeeded"/> or <see cref="Failed"/>.
    /// </summary>
    public required string Result { get; init; }

    /// <summary>
    /// Gets what went wrong, for a deployment that failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets when the deployment ended.
    /// </summary>
    public required DateTimeOffset Time { get; init; }

    /// <summary>
    /// Gets the machine that deployed.
    /// </summary>
    public string? Machine { get; init; }

    /// <summary>
    /// Gets the build that deployed, as in <c>16473829-1</c>, when it ran in one.
    /// </summary>
    public string? BuildId { get; init; }
}

/// <summary>
/// Represents the record of every deployment, kept as JSON lines in the telemetry layout,
/// <c>deployment/date=&lt;day&gt;/</c>, so it can be queried beside the builds that made the drops.
/// </summary>
/// <param name="root">The directory the ledger is kept in.</param>
internal sealed class Ledger(string root)
{
    /// <summary>
    /// The table the rows go to.
    /// </summary>
    public const string Table = "deployment";

    /// <summary>
    /// Adds rows to the ledger.
    /// </summary>
    /// <param name="deployments">The rows, all of one deployment.</param>
    public void Record(IReadOnlyList<DeploymentInfo> deployments)
    {
        if (deployments.Count == 0)
            return;

        var first = deployments[0];
        TelemetryWriter.Write(root, Table, $"{first.Drop}.{first.Target}", first.Time, deployments, LedgerJsonContext.Default.DeploymentInfo);
    }

    /// <summary>
    /// Returns every row, oldest first.
    /// </summary>
    /// <returns>The rows.</returns>
    public IReadOnlyList<DeploymentInfo> All() =>
        [.. TelemetryWriter.Read(root, Table, "", LedgerJsonContext.Default.DeploymentInfo).OrderBy(deployment => deployment.Time)];

    /// <summary>
    /// Returns the last time an artifact went to a target and the deployment went through.
    /// </summary>
    /// <param name="target">The target.</param>
    /// <param name="artifact">The artifact's name.</param>
    /// <returns>The row, or <see langword="null"/> when the artifact never went there.</returns>
    public DeploymentInfo? LastSucceeded(string target, string artifact) =>
        All().LastOrDefault(deployment => deployment.Target == target && deployment.Artifact == artifact && deployment.Result == DeploymentInfo.Succeeded);
}

/// <summary>
/// Provides the generated serialization of <see cref="DeploymentInfo"/>.
/// </summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(DeploymentInfo))]
internal sealed partial class LedgerJsonContext : JsonSerializerContext;
