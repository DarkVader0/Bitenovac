namespace Bitenovac.RemoteDeployTool;

/// <summary>
/// Provides the preparation of a published web app as a GitHub Pages site.
/// </summary>
/// <remarks>
/// <para>
/// The app is served from the root of its own domain, so its <c>index.html</c> keeps
/// <c>&lt;base href="/"&gt;</c>, and a staging copy is too, on a domain of its own.
/// </para>
/// <list type="bullet">
/// <item>Pages runs Jekyll unless told not to, and Jekyll drops <c>_framework</c>; <c>.nojekyll</c> turns it off.</item>
/// <item>
/// Pages answers a path it has no file for with <c>404.html</c>, so that is a copy of <c>index.html</c>,
/// and the app routes a reloaded deep link itself.
/// </item>
/// <item>The precompressed <c>.br</c> and <c>.gz</c> copies are left out: Pages compresses on its own and would serve them only as downloads.</item>
/// </list>
/// </remarks>
internal static class PagesSite
{
    private const string WebRoot = "wwwroot";
    private const string IndexFile = "index.html";
    private static readonly string[] Precompressed = [".br", ".gz"];

    /// <summary>
    /// Writes the app <paramref name="appName"/> of a drop into <paramref name="siteDirectory"/> as a Pages site.
    /// </summary>
    /// <param name="dropDirectory">The drop.</param>
    /// <param name="appName">The app, as the drop's manifest names it.</param>
    /// <param name="siteDirectory">An empty or missing directory to write the site into.</param>
    /// <param name="output">Where the preparation reports.</param>
    /// <returns>Zero when the site is ready; one otherwise.</returns>
    /// <exception cref="InvalidOperationException">The drop has no manifest.</exception>
    public static int Prepare(string dropDirectory, string appName, string siteDirectory, PipelineOutput output)
    {
        var manifest = DropManifest.Read(dropDirectory);
        var app = manifest.Artifacts.FirstOrDefault(artifact => artifact.Kind == DropArtifact.App && artifact.Name == appName);
        var webRoot = app is null ? null : Path.Combine(dropDirectory, app.Path, WebRoot);
        if (webRoot is null || !File.Exists(Path.Combine(webRoot, IndexFile)))
        {
            output.WriteFailure($"drop {manifest.Version} has no published web app {appName}: no {WebRoot}/{IndexFile} under apps/{appName}.");
            return 1;
        }

        if (Directory.Exists(siteDirectory) && Directory.EnumerateFileSystemEntries(siteDirectory).Any())
        {
            output.WriteFailure($"{siteDirectory} is not empty; the site is written only into an empty or missing directory.");
            return 1;
        }

        output.WriteLine($"==> Preparing {appName} from drop {manifest.Version}");
        var files = 0;
        foreach (var file in Directory.EnumerateFiles(webRoot, "*", SearchOption.AllDirectories))
        {
            if (Precompressed.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                continue;

            var destination = Path.Combine(siteDirectory, Path.GetRelativePath(webRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
            files++;
        }

        File.Copy(Path.Combine(siteDirectory, IndexFile), Path.Combine(siteDirectory, "404.html"));
        File.WriteAllText(Path.Combine(siteDirectory, ".nojekyll"), "");

        output.WriteLine($"{files + 2} files ready in {siteDirectory}");
        return 0;
    }
}
