namespace Bitenovac.RemoteDeployTool;

/// <summary>
/// Provides the deployment of a drop's web app to a Cloudflare Pages project, through Wrangler.
/// </summary>
/// <remarks>
/// The site uploaded is the one <see cref="PagesSite"/> prepares for GitHub Pages, so what a staging
/// project on Cloudflare serves is what production serves once it is promoted. Wrangler runs through
/// <c>npx</c>, so the machine needs Node 22.12 or later, and it authenticates from
/// <c>CLOUDFLARE_API_TOKEN</c> and <c>CLOUDFLARE_ACCOUNT_ID</c> in the environment.
/// </remarks>
internal static class CloudflarePagesDeploy
{
    /// <summary>
    /// The Wrangler release the deployment runs, pinned so a new release cannot change a deployment
    /// unnoticed.
    /// </summary>
    public const string Wrangler = "wrangler@4.155.0";

    /// <summary>
    /// Returns the ledger's name for a branch of a Cloudflare Pages project.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="branch">The branch.</param>
    /// <returns>The target, as in <c>cloudflare-pages:planner-staging/main</c>.</returns>
    public static string Target(string project, string branch) => $"cloudflare-pages:{project}/{branch}";

    /// <summary>
    /// Deploys a drop's web app to a branch of a Cloudflare Pages project.
    /// </summary>
    /// <param name="dropDirectory">The drop.</param>
    /// <param name="appName">The app, as the drop's manifest names it.</param>
    /// <param name="project">The Cloudflare Pages project, which must exist.</param>
    /// <param name="branch">The branch; the project's production branch makes it the project's live site.</param>
    /// <param name="options">The ledger the deployment is recorded in.</param>
    /// <param name="services">What runs Wrangler, and the clock.</param>
    /// <param name="output">Where the deployment reports.</param>
    /// <returns>Zero when the site went up; one otherwise.</returns>
    /// <exception cref="InvalidOperationException">The drop has no manifest.</exception>
    public static int Run(
        string dropDirectory, string appName, string project, string branch, DeployOptions options, DeployServices services, PipelineOutput output)
    {
        var manifest = DropManifest.Read(dropDirectory);
        var site = Directory.CreateTempSubdirectory("remotedeploytool-site-").FullName;
        try
        {
            if (PagesSite.Prepare(dropDirectory, appName, site, output) != 0)
                return 1;

            List<string> arguments = ["--yes", Wrangler, "pages", "deploy", site, "--project-name", project, "--branch", branch];
            if (manifest.Sha is { } sha)
                arguments.AddRange("--commit-hash", sha);
            arguments.AddRange("--commit-message", $"Drop {manifest.Version}");

            output.WriteLine($"==> Deploying {appName} from drop {manifest.Version} to {project} ({branch})");
            var (exitCode, said) = services.Run("npx", arguments, site);
            output.Out.Write(said);

            var app = manifest.Artifacts.Single(artifact => artifact.Kind == DropArtifact.App && artifact.Name == appName);
            var error = exitCode == 0 ? null : $"wrangler exited with {exitCode}";
            options.Ledger?.Record([options.Deployment(Target(project, branch), manifest, app, services.Time.GetUtcNow(), error)]);

            if (error is not null)
            {
                output.WriteFailure($"{appName} from drop {manifest.Version} did not reach {project} ({branch}): {error}.");
                return 1;
            }

            return 0;
        }
        finally
        {
            Directory.Delete(site, recursive: true);
        }
    }
}
