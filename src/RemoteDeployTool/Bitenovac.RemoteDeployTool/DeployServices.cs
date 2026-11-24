namespace Bitenovac.RemoteDeployTool;

/// <summary>
/// Represents what the deploy tool reaches outside itself through: the feeds it pushes to, the
/// programs it runs and the clock.
/// </summary>
internal sealed record DeployServices
{
    /// <summary>
    /// Gets the NuGet feed to push to, given the options.
    /// </summary>
    public Func<DeployOptions, IPackageFeed> FeedFor { get; init; } =
        options => new DotNetPackageFeed(options.NuGetSource, options.NuGetApiKey);

    /// <summary>
    /// Gets what runs a program: given its name, its arguments and where it runs, it returns its exit
    /// code and output.
    /// </summary>
    public Func<string, IReadOnlyList<string>, string, (int ExitCode, string Output)> Run { get; init; } =
        (fileName, arguments, workingDirectory) => ChildProcess.Run(fileName, arguments, workingDirectory);

    /// <summary>
    /// Gets the clock deployments are recorded by.
    /// </summary>
    public TimeProvider Time { get; init; } = TimeProvider.System;
}
