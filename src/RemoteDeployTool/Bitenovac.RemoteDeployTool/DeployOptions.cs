namespace Bitenovac.RemoteDeployTool;

/// <summary>
/// Represents where the deploy tool finds what it deploys, and where it deploys it.
/// </summary>
/// <param name="DropRoot">The directory that holds one subdirectory per release drop.</param>
/// <param name="NuGetSource">The NuGet feed packages are pushed to.</param>
/// <param name="NuGetApiKey">The key the feed takes, if it takes one.</param>
internal sealed record DeployOptions(string DropRoot, string NuGetSource, string? NuGetApiKey)
{
    /// <summary>
    /// The feed packages go to unless told otherwise.
    /// </summary>
    public const string NuGetOrg = "https://api.nuget.org/v3/index.json";

    /// <summary>
    /// Reads the options from the environment.
    /// </summary>
    /// <param name="read">Reads an environment variable.</param>
    /// <param name="currentDirectory">The directory the tool runs in.</param>
    /// <returns>
    /// The options: the drop root is <c>REMOTEDEPLOYTOOL_DROP_ROOT</c>, or <c>artifacts/official</c>
    /// under <paramref name="currentDirectory"/>, where a RemoteBuildTool release drops by default.
    /// The feed is <c>REMOTEDEPLOYTOOL_NUGET_SOURCE</c>, or nuget.org, and its key <c>NUGET_API_KEY</c>.
    /// </returns>
    public static DeployOptions FromEnvironment(Func<string, string?> read, string currentDirectory)
    {
        string? Value(string name) => read(name) is { Length: > 0 } value ? value : null;

        return new DeployOptions(
            Value("REMOTEDEPLOYTOOL_DROP_ROOT") ?? Path.Combine(currentDirectory, "artifacts", "official"),
            Value("REMOTEDEPLOYTOOL_NUGET_SOURCE") ?? NuGetOrg,
            Value("NUGET_API_KEY"));
    }
}
