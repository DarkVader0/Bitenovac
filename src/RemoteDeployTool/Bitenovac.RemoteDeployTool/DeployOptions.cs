namespace Bitenovac.RemoteDeployTool;

/// <summary>
/// Represents where the deploy tool finds what it deploys, where it deploys it, and where it records that.
/// </summary>
/// <param name="DropRoot">The directory that holds one subdirectory per release drop.</param>
/// <param name="NuGetSource">The NuGet feed packages are pushed to.</param>
/// <param name="NuGetApiKey">The key the feed takes, if it takes one.</param>
/// <param name="LedgerRoot">The directory the ledger of deployments is kept in; none is kept when <see langword="null"/>.</param>
/// <param name="Machine">The machine that deploys, as the ledger names it.</param>
/// <param name="BuildId">The build that deploys, as in <c>16473829-1</c>, when it runs in one.</param>
internal sealed record DeployOptions(string DropRoot, string NuGetSource, string? NuGetApiKey, string? LedgerRoot = null, string? Machine = null, string? BuildId = null)
{
    /// <summary>
    /// The feed packages go to unless told otherwise.
    /// </summary>
    public const string NuGetOrg = "https://api.nuget.org/v3/index.json";

    /// <summary>
    /// Gets the ledger, or <see langword="null"/> when none is kept.
    /// </summary>
    public Ledger? Ledger => LedgerRoot is null ? null : new Ledger(LedgerRoot);

    /// <summary>
    /// Reads the options from the environment.
    /// </summary>
    /// <param name="read">Reads an environment variable.</param>
    /// <param name="currentDirectory">The directory the tool runs in.</param>
    /// <returns>
    /// The options: the drop root is <c>REMOTEDEPLOYTOOL_DROP_ROOT</c>, or <c>artifacts/official</c>
    /// under <paramref name="currentDirectory"/>, where a RemoteBuildTool release drops by default.
    /// The feed is <c>REMOTEDEPLOYTOOL_NUGET_SOURCE</c>, or nuget.org, and its key <c>NUGET_API_KEY</c>.
    /// The ledger is kept in <c>REMOTEDEPLOYTOOL_LEDGER_DIR</c>; the machine is
    /// <c>REMOTEDEPLOYTOOL_MACHINE</c>, or this one's name; the build is GitHub's run and attempt.
    /// </returns>
    public static DeployOptions FromEnvironment(Func<string, string?> read, string currentDirectory)
    {
        string? Value(string name) => read(name) is { Length: > 0 } value ? value : null;

        return new DeployOptions(
            Value("REMOTEDEPLOYTOOL_DROP_ROOT") ?? Path.Combine(currentDirectory, "artifacts", "official"),
            Value("REMOTEDEPLOYTOOL_NUGET_SOURCE") ?? NuGetOrg,
            Value("NUGET_API_KEY"),
            Value("REMOTEDEPLOYTOOL_LEDGER_DIR"),
            Value("REMOTEDEPLOYTOOL_MACHINE") ?? Environment.MachineName,
            Value("GITHUB_RUN_ID") is { } run ? $"{run}-{Value("GITHUB_RUN_ATTEMPT") ?? "1"}" : null);
    }

    /// <summary>
    /// Returns a ledger row for an artifact of a drop, deployed to a target.
    /// </summary>
    /// <param name="target">Where it went.</param>
    /// <param name="manifest">The drop's manifest.</param>
    /// <param name="artifact">The artifact.</param>
    /// <param name="time">When the deployment ended.</param>
    /// <param name="error">What went wrong, or <see langword="null"/> when it went through.</param>
    /// <returns>The row.</returns>
    public DeploymentInfo Deployment(string target, DropManifest manifest, DropArtifact artifact, DateTimeOffset time, string? error = null) => new()
    {
        Target = target,
        Kind = artifact.Kind,
        Artifact = artifact.Name,
        Version = artifact.Version,
        Drop = manifest.Version,
        Sha = manifest.Sha,
        Fingerprint = artifact.Fingerprint,
        Result = error is null ? DeploymentInfo.Succeeded : DeploymentInfo.Failed,
        Error = error,
        Time = time,
        Machine = Machine,
        BuildId = BuildId,
    };
}
