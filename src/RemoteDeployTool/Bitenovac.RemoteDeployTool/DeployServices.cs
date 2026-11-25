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
    /// Gets the client GitHub is reached through.
    /// </summary>
    public Func<HttpClient> Http { get; init; } = () => new HttpClient();

    /// <summary>
    /// Gets how long staging is given to serve a deployment before the deployment is rolled back.
    /// </summary>
    public TimeSpan StagingPatience { get; init; } = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Gets how long to wait between two checks of staging.
    /// </summary>
    public TimeSpan StagingPollInterval { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets the clock deployments are recorded by.
    /// </summary>
    public TimeProvider Time { get; init; } = TimeProvider.System;
}
