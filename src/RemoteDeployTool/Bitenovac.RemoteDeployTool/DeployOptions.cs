namespace Bitenovac.RemoteDeployTool;

/// <summary>
/// Represents where the deploy tool finds what it deploys.
/// </summary>
/// <param name="DropRoot">The directory that holds one subdirectory per release drop.</param>
internal sealed record DeployOptions(string DropRoot)
{
    /// <summary>
    /// Reads the options from the environment.
    /// </summary>
    /// <param name="read">Reads an environment variable.</param>
    /// <param name="currentDirectory">The directory the tool runs in.</param>
    /// <returns>
    /// The options: the drop root is <c>REMOTEDEPLOYTOOL_DROP_ROOT</c>, or <c>artifacts/official</c>
    /// under <paramref name="currentDirectory"/>, where a RemoteBuildTool release drops by default.
    /// </returns>
    public static DeployOptions FromEnvironment(Func<string, string?> read, string currentDirectory) =>
        new(read("REMOTEDEPLOYTOOL_DROP_ROOT") is { Length: > 0 } root
            ? root
            : Path.Combine(currentDirectory, "artifacts", "official"));
}
