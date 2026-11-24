namespace Bitenovac.RemoteDeployTool;

/// <summary>
/// Provides the deployment of pending drops to the staging site, the check that staging serves what
/// was deployed, and the rollback to the last drop that passed when it does not.
/// </summary>
internal static class StagingDeploy
{
    /// <summary>
    /// The web app staging serves.
    /// </summary>
    public const string WebApp = "Bitenovac.DecompressionCalculator.Web";

    /// <summary>
    /// The Cloudflare Pages project staging is.
    /// </summary>
    public const string Project = "planner-staging";

    /// <summary>
    /// The branch staging deploys to.
    /// </summary>
    public const string Branch = "main";

    private const string PendingDirectory = "pending";
    private const string VersionFile = "drop.txt";
    private static readonly string Target = CloudflarePagesDeploy.Target(Project, Branch);

    /// <summary>
    /// Marks a drop as waiting for staging: Official does this when it makes a drop, and the deploy
    /// tool picks it up the next time it runs, so a drop is never lost when nothing was listening.
    /// </summary>
    /// <param name="options">The ledger the mark is kept in.</param>
    /// <param name="drop">The drop.</param>
    /// <param name="output">Where the mark is reported.</param>
    /// <returns>Zero.</returns>
    /// <exception cref="InvalidOperationException">The drop does not exist, or no ledger is kept.</exception>
    public static int Pend(DeployOptions options, string drop, PipelineOutput output)
    {
        Drops.Resolve(options, drop);
        var directory = Path.Combine(LedgerOf(options), PendingDirectory);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, drop), string.Empty);
        output.WriteLine($"Drop {drop} waits for staging.");
        return 0;
    }

    /// <summary>
    /// Lists the drops waiting for staging, the oldest first.
    /// </summary>
    /// <param name="options">The ledger the marks are kept in.</param>
    /// <returns>The drops; none when no ledger is kept.</returns>
    public static IReadOnlyList<string> Pending(DeployOptions options)
    {
        if (options.LedgerRoot is null)
            return [];

        var directory = Path.Combine(options.LedgerRoot, PendingDirectory);
        return Directory.Exists(directory)
            ? [.. Directory.EnumerateFiles(directory).Select(file => Path.GetFileName(file)!).OrderBy(name => name, StringComparer.Ordinal)]
            : [];
    }

    /// <summary>
    /// Deploys each drop to staging and checks that staging serves it. A drop that is not served
    /// rolls staging back to the last drop that was.
    /// </summary>
    /// <param name="options">The drop root, the ledger and where staging is served.</param>
    /// <param name="services">What runs Wrangler and makes the requests, and the clock.</param>
    /// <param name="drops">The drops, in the order they are deployed.</param>
    /// <param name="output">Where the deployments report.</param>
    /// <returns>Zero when every drop was served; one otherwise.</returns>
    /// <exception cref="InvalidOperationException">A drop does not exist or has no web app, or staging's URL is not set.</exception>
    public static async Task<int> RunAsync(DeployOptions options, DeployServices services, IReadOnlyList<string> drops, PipelineOutput output)
    {
        var failed = 0;
        foreach (var drop in drops)
        {
            if (!await DeployAsync(options, services, drop, output, rollingBack: false))
                failed++;

            RemovePending(options, drop);
        }

        return failed == 0 ? 0 : 1;
    }

    private static async Task<bool> DeployAsync(DeployOptions options, DeployServices services, string drop, PipelineOutput output, bool rollingBack)
    {
        var directory = Drops.Resolve(options, drop);
        var manifest = DropManifest.Read(directory);
        var app = manifest.Artifacts.Single(artifact => artifact.Kind == DropArtifact.App && artifact.Name == WebApp);

        // Recorded below, once the site is checked: a deployment that was not served is not one to roll back to.
        var deployed = CloudflarePagesDeploy.Run(directory, WebApp, Project, Branch, options with { LedgerRoot = null }, services, output) == 0;
        var served = deployed && await ServesAsync(options, services, manifest.Version);
        var error = served ? null : deployed ? $"staging does not serve drop {manifest.Version}" : "the deploy did not go through";
        options.Ledger?.Record([options.Deployment(Target, manifest, app, services.Time.GetUtcNow(), error)]);
        if (served)
        {
            output.WriteLine($"Staging serves drop {manifest.Version}.");
            return true;
        }

        output.WriteFailure($"{error}.");
        if (!rollingBack)
            await RollBackAsync(options, services, output);

        return false;
    }

    private static async Task RollBackAsync(DeployOptions options, DeployServices services, PipelineOutput output)
    {
        var last = options.Ledger?.LastSucceeded(Target, WebApp);
        if (last is null)
        {
            output.WriteFailure("there is no drop staging served before to roll back to.");
            return;
        }

        output.WriteLine($"==> Rolling staging back to drop {last.Drop}");
        await DeployAsync(options, services, last.Drop, output, rollingBack: true);
    }

    private static async Task<bool> ServesAsync(DeployOptions options, DeployServices services, string version)
    {
        if (options.StagingUrl is null)
            throw new InvalidOperationException("REMOTEDEPLOYTOOL_STAGING_URL is not set, so the staging site cannot be checked.");

        using var client = services.Http();
        try
        {
            var response = await client.GetAsync($"{options.StagingUrl.TrimEnd('/')}/{VersionFile}");
            return response.IsSuccessStatusCode && (await response.Content.ReadAsStringAsync()).Trim() == version;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private static void RemovePending(DeployOptions options, string drop)
    {
        var marker = options.LedgerRoot is null ? null : Path.Combine(options.LedgerRoot, PendingDirectory, drop);
        if (marker is not null && File.Exists(marker))
            File.Delete(marker);
    }

    private static string LedgerOf(DeployOptions options) =>
        options.LedgerRoot ?? throw new InvalidOperationException("REMOTEDEPLOYTOOL_LEDGER_DIR is not set, so a drop waiting for staging has nowhere to wait.");
}
