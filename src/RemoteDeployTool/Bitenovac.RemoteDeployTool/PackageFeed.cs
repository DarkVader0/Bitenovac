namespace Bitenovac.RemoteDeployTool;

/// <summary>
/// Specifies what pushing a package did.
/// </summary>
internal enum PushOutcome
{
    /// <summary>
    /// The feed took the package.
    /// </summary>
    Pushed,

    /// <summary>
    /// The feed already had the package at that version, and kept what it had.
    /// </summary>
    AlreadyThere,
}

/// <summary>
/// Represents a NuGet feed packages are pushed to.
/// </summary>
internal interface IPackageFeed
{
    /// <summary>
    /// Pushes one package, unless the feed already has its version.
    /// </summary>
    /// <param name="packagePath">The package file.</param>
    /// <param name="id">The package's id.</param>
    /// <param name="version">The package's version.</param>
    /// <returns>What the push did.</returns>
    /// <exception cref="InvalidOperationException">The feed refused the package, or could not be reached.</exception>
    PushOutcome Push(string packagePath, string id, string version);
}

/// <summary>
/// Represents a NuGet feed pushed to with <c>dotnet nuget push</c>.
/// </summary>
/// <param name="source">The feed: a URL, or a directory.</param>
/// <param name="apiKey">The key the feed takes, if it takes one.</param>
/// <param name="run">
/// Runs <c>dotnet</c> with the given arguments in the given directory and returns its exit code and
/// output; <see cref="ChildProcess.Run"/> when omitted.
/// </param>
internal sealed class DotNetPackageFeed(string source, string? apiKey, Func<IReadOnlyList<string>, string, (int ExitCode, string Output)>? run = null) : IPackageFeed
{
    private readonly Func<IReadOnlyList<string>, string, (int ExitCode, string Output)> _run =
        run ?? ((arguments, workingDirectory) => ChildProcess.Run("dotnet", arguments, workingDirectory));

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// A version a feed on the web already has is skipped rather than refused. nuget.org can take a
    /// package and still time out before saying so; the client then retries and is told the version
    /// exists, which a skip turns into what it was.
    /// </para>
    /// <para>
    /// A directory feed cannot skip: <c>dotnet nuget push</c> replaces what it holds. A version it
    /// already has, in either of its layouts, is therefore left alone without pushing.
    /// </para>
    /// </remarks>
    public PushOutcome Push(string packagePath, string id, string version)
    {
        if (!IsOnTheWeb(source) && HasVersion(id, version))
            return PushOutcome.AlreadyThere;

        List<string> arguments = ["nuget", "push", packagePath, "--source", source, "--skip-duplicate"];
        if (apiKey is not null)
            arguments.AddRange("--api-key", apiKey);

        var (exitCode, output) = _run(arguments, Path.GetDirectoryName(packagePath)!);
        if (exitCode != 0)
            throw new InvalidOperationException($"pushing {Path.GetFileName(packagePath)} failed (exit {exitCode}):{Environment.NewLine}{output.TrimEnd()}");

        return output.Contains("already exists", StringComparison.OrdinalIgnoreCase) ? PushOutcome.AlreadyThere : PushOutcome.Pushed;
    }

    /// <summary>
    /// Returns whether a feed is on the web, rather than a directory.
    /// </summary>
    /// <param name="feed">The feed.</param>
    /// <returns><see langword="true"/> for an http or https URL.</returns>
    public static bool IsOnTheWeb(string feed) =>
        Uri.TryCreate(feed, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    // A flat directory feed holds <id>.<version>.nupkg; a hierarchical one <id>/<version>/, lower case.
    private bool HasVersion(string id, string version) =>
        File.Exists(Path.Combine(source, $"{id}.{version}.nupkg"))
        || Directory.Exists(Path.Combine(source, id.ToLowerInvariant(), version.ToLowerInvariant()));
}
