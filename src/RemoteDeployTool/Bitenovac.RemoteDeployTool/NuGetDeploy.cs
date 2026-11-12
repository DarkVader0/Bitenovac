namespace Bitenovac.RemoteDeployTool;

/// <summary>
/// Provides the deployment of a drop's packages to a NuGet feed.
/// </summary>
internal static class NuGetDeploy
{
    /// <summary>
    /// Pushes the packages of a drop that match <paramref name="patterns"/>.
    /// </summary>
    /// <param name="dropDirectory">The drop.</param>
    /// <param name="patterns">
    /// The packages to push: an id, or a prefix ending in <c>.*</c> that takes every package whose id
    /// starts with it, such as <c>Bitenovac.DecompressionCalculator.*</c>.
    /// </param>
    /// <param name="sameVersion">Whether the packages must all have one version, as a tool and its service do.</param>
    /// <param name="feed">Where they go.</param>
    /// <param name="output">Where the deployment reports.</param>
    /// <returns>Zero when at least one package was pushed; one otherwise.</returns>
    /// <exception cref="InvalidOperationException">The drop has no manifest, or the feed refused a package.</exception>
    /// <remarks>
    /// <para>
    /// Nothing is pushed unless every chosen package passes the checks first. A version that starts
    /// with a year is the drop's date, given to a project that has no version of its own; a feed
    /// never deletes a version, and that one would outrank every real one.
    /// </para>
    /// <para>
    /// A package the feed already has is skipped, so a deployment that stopped part way can be run
    /// again for the rest. One in which the feed already has every package fails, so a version that
    /// was not raised fails loudly.
    /// </para>
    /// </remarks>
    public static int Run(string dropDirectory, IReadOnlyList<string> patterns, bool sameVersion, IPackageFeed feed, PipelineOutput output)
    {
        var manifest = DropManifest.Read(dropDirectory);
        var packages = manifest.Artifacts
            .Where(artifact => artifact.Kind == DropArtifact.Package && patterns.Any(pattern => Matches(pattern, artifact.Name)))
            .ToList();

        if (packages.Count == 0)
        {
            output.WriteFailure($"drop {manifest.Version} has no package that matches {string.Join(", ", patterns)}.");
            return 1;
        }

        if (packages.FirstOrDefault(package => IsDropDate(package.Version!)) is { } dated)
        {
            output.WriteFailure($"{dated.Name} {dated.Version} carries the drop's date, not a version of its own; give the project a <VersionPrefix>.");
            return 1;
        }

        if (sameVersion && packages.Select(package => package.Version).Distinct(StringComparer.Ordinal).Count() > 1)
        {
            output.WriteFailure($"the packages must share one version, but they are {string.Join(", ", packages.Select(package => $"{package.Name} {package.Version}"))}.");
            return 1;
        }

        var pushed = 0;
        foreach (var package in packages)
        {
            output.WriteLine($"==> Pushing {package.Name} {package.Version} from drop {manifest.Version}");
            var outcome = feed.Push(Path.Combine(dropDirectory, package.Path), package.Name, package.Version!);
            output.WriteLine(outcome == PushOutcome.Pushed ? "    pushed" : "    already on the feed; skipped");
            if (outcome == PushOutcome.Pushed)
                pushed++;
        }

        if (pushed == 0)
        {
            output.WriteFailure($"the feed already has every package of drop {manifest.Version} that matches {string.Join(", ", patterns)}; raise their <VersionPrefix>.");
            return 1;
        }

        output.WriteLine(pushed == packages.Count
            ? $"Pushed {pushed} package(s)."
            : $"Pushed {pushed} of {packages.Count} package(s); the feed already had the rest.");
        return 0;
    }

    private static bool Matches(string pattern, string id) =>
        pattern.EndsWith(".*", StringComparison.Ordinal)
            ? id.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase)
            : string.Equals(pattern, id, StringComparison.OrdinalIgnoreCase);

    private static bool IsDropDate(string version) =>
        version.Split('.')[0] is { Length: 4 } year && year.All(char.IsAsciiDigit);
}
