using System.Globalization;

namespace Bitenovac.RemoteDeployTool;

/// <summary>
/// Provides the deploy tool's commands.
/// </summary>
internal static class CommandLine
{
    /// <summary>
    /// Runs the command <paramref name="args"/> name.
    /// </summary>
    /// <param name="args">The command and its arguments.</param>
    /// <param name="output">Where the command reports.</param>
    /// <param name="read">Reads an environment variable.</param>
    /// <param name="services">What the tool reaches outside itself through; the real feeds, programs and clock when omitted.</param>
    /// <returns>Zero on success, one on failure.</returns>
    public static int Run(string[] args, PipelineOutput output, Func<string, string?> read, DeployServices? services = null)
    {
        var options = DeployOptions.FromEnvironment(read, Directory.GetCurrentDirectory());
        services ??= new DeployServices();
        try
        {
            return args switch
            {
                [] => Usage(output, 1),
                ["drops"] => ListDrops(options, output),
                ["status"] => Status(options, output),
                ["show"] => Show(options, null, output),
                ["show", var drop] => Show(options, drop, output),
                ["changes", var from] => Changes(options, from, null, output),
                ["changes", var from, var to] => Changes(options, from, to, output),
                ["nuget", .. var rest] => NuGet(options, rest, output, services),
                ["record", var target, var artifact, .. var rest] => Record(options, target, artifact, rest, output, services),
                ["pages", var app, var site] => PagesSite.Prepare(Drops.Resolve(options, null), app, site, output),
                ["pages", var app, var site, "--drop", var drop] => PagesSite.Prepare(Drops.Resolve(options, drop), app, site, output),
                ["pages", var app, var site, "--from", var target] => PagesSite.Prepare(Promoted(options, target, app), app, site, output),
                ["cloudflare", var app, .. var rest] => Cloudflare(options, app, rest, read, output, services),
                ["--help" or "-h"] => Usage(output, 0),
                ["--version"] => Version(output),
                _ => Fail(output, $"'{string.Join(' ', args)}' is not a command. Run 'remotedeploytool --help'."),
            };
        }
        catch (InvalidOperationException exception)
        {
            return Fail(output, exception.Message);
        }
    }

    private static int ListDrops(DeployOptions options, PipelineOutput output)
    {
        var drops = Drops.All(options);
        if (drops.Count == 0)
        {
            output.WriteLine($"There are no drops in {options.DropRoot}.");
            return 0;
        }

        foreach (var name in drops)
        {
            var directory = Path.Combine(options.DropRoot, name);
            output.WriteLine(File.Exists(Path.Combine(directory, DropManifest.FileName))
                ? Describe(DropManifest.Read(directory))
                : $"{name}  no manifest");
        }

        return 0;
    }

    private static int Show(DeployOptions options, string? drop, PipelineOutput output)
    {
        var manifest = DropManifest.Read(Drops.Resolve(options, drop));
        output.WriteLine(Describe(manifest));
        foreach (var artifact in manifest.Artifacts)
            output.WriteLine($"  {artifact.Kind,-8} {Label(artifact.Name, artifact.Version)}  {artifact.Sha256[..12]}");

        return 0;
    }

    private static int Changes(DeployOptions options, string from, string? to, PipelineOutput output)
    {
        var older = DropManifest.Read(Drops.Resolve(options, from));
        var newer = DropManifest.Read(Drops.Resolve(options, to));
        var changes = DropChanges.Compare(older, newer);

        output.WriteLine($"From {older.Version} to {newer.Version}:");
        foreach (var change in changes)
        {
            var version = change.Change == ArtifactChange.Removed ? change.From!.Version : change.To!.Version;
            output.WriteLine($"  {change.Change.ToString().ToLowerInvariant(),-9} {change.Kind,-8} {Label(change.Name, version)}");
        }

        int Count(ArtifactChange kind) => changes.Count(change => change.Change == kind);
        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Count(ArtifactChange.Added) + Count(ArtifactChange.Changed)} to deploy: {Count(ArtifactChange.Added)} added, {Count(ArtifactChange.Changed)} changed; {Count(ArtifactChange.Unchanged)} unchanged, {Count(ArtifactChange.Removed)} removed."));
        return 0;
    }

    private static int NuGet(DeployOptions options, string[] arguments, PipelineOutput output, DeployServices services)
    {
        string? drop = null;
        var sameVersion = false;
        var patterns = new List<string>();
        for (var index = 0; index < arguments.Length; index++)
        {
            switch (arguments[index])
            {
                case "--drop" when index + 1 < arguments.Length:
                    drop = arguments[++index];
                    break;
                case "--same-version":
                    sameVersion = true;
                    break;
                case var option when option.StartsWith("--", StringComparison.Ordinal):
                    return Fail(output, $"'{option}' is not an option of nuget, or it needs a value. Run 'remotedeploytool --help'.");
                case var pattern:
                    patterns.Add(pattern);
                    break;
            }
        }

        if (patterns.Count == 0)
            return Fail(output, "name the packages to push, as an id or a prefix such as Bitenovac.DecompressionCalculator.*.");

        if (options.NuGetApiKey is null && DotNetPackageFeed.IsOnTheWeb(options.NuGetSource))
            return Fail(output, $"NUGET_API_KEY is not set; {options.NuGetSource} takes packages only with a key.");

        return NuGetDeploy.Run(Drops.Resolve(options, drop), patterns, sameVersion, services.FeedFor(options), output, options, services.Time);
    }

    // Promoting takes what a target, such as staging, last got and went through.
    private static string Promoted(DeployOptions options, string target, string app)
    {
        var ledger = options.Ledger ?? throw new InvalidOperationException("REMOTEDEPLOYTOOL_LEDGER_DIR is not set, so there is no ledger to promote from.");
        var deployed = ledger.LastSucceeded(target, app)
            ?? throw new InvalidOperationException($"{app} has never gone through to {target}, so there is nothing to promote.");

        return Drops.Resolve(options, deployed.Drop);
    }

    private static int Cloudflare(DeployOptions options, string app, string[] arguments, Func<string, string?> read, PipelineOutput output, DeployServices services)
    {
        string? project = null;
        var branch = "main";
        string? drop = null;
        for (var index = 0; index < arguments.Length; index++)
        {
            switch (arguments[index])
            {
                case "--project" when index + 1 < arguments.Length:
                    project = arguments[++index];
                    break;
                case "--branch" when index + 1 < arguments.Length:
                    branch = arguments[++index];
                    break;
                case "--drop" when index + 1 < arguments.Length:
                    drop = arguments[++index];
                    break;
                case var unknown:
                    return Fail(output, $"'{unknown}' is not an option of cloudflare, or it needs a value. Run 'remotedeploytool --help'.");
            }
        }

        if (project is null)
            return Fail(output, "name the Cloudflare Pages project with --project.");

        var missing = new[] { "CLOUDFLARE_API_TOKEN", "CLOUDFLARE_ACCOUNT_ID" }.Where(name => read(name) is not { Length: > 0 }).ToList();
        if (missing.Count > 0)
            return Fail(output, $"{string.Join(" and ", missing)} must be set for Wrangler to reach Cloudflare.");

        return CloudflarePagesDeploy.Run(Drops.Resolve(options, drop), app, project, branch, options, services, output);
    }

    private static int Status(DeployOptions options, PipelineOutput output)
    {
        var ledger = options.Ledger ?? throw new InvalidOperationException("REMOTEDEPLOYTOOL_LEDGER_DIR is not set, so no ledger of deployments is kept.");
        var latest = ledger.All()
            .GroupBy(deployment => (deployment.Target, deployment.Kind, deployment.Artifact))
            .OrderBy(group => group.Key.Target, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Artifact, StringComparer.Ordinal)
            .ToList();
        if (latest.Count == 0)
        {
            output.WriteLine($"No deployments are recorded in {options.LedgerRoot}.");
            return 0;
        }

        foreach (var group in latest)
        {
            // The rows are oldest first, so the last of a group is where its artifact stands.
            var last = group.Last();
            var line = string.Create(
                CultureInfo.InvariantCulture,
                $"{group.Key.Target}  {group.Key.Kind} {Label(group.Key.Artifact, last.Version)}  {last.Drop}  {last.Result}  {last.Time:yyyy-MM-dd HH:mm}Z");
            if (last.Result == DeploymentInfo.Failed)
            {
                line += group.LastOrDefault(deployment => deployment.Result == DeploymentInfo.Succeeded) is { } good
                    ? $"; {good.Drop} is the last that went through"
                    : "; none has gone through";
            }

            output.WriteLine(line);
        }

        return 0;
    }

    private static int Record(DeployOptions options, string target, string name, string[] arguments, PipelineOutput output, DeployServices services)
    {
        string? drop = null;
        string? error = null;
        for (var index = 0; index < arguments.Length; index++)
        {
            switch (arguments[index])
            {
                case "--drop" when index + 1 < arguments.Length:
                    drop = arguments[++index];
                    break;
                case "--failed" when index + 1 < arguments.Length:
                    error = arguments[++index];
                    break;
                case var unknown:
                    return Fail(output, $"'{unknown}' is not an option of record, or it needs a value. Run 'remotedeploytool --help'.");
            }
        }

        var ledger = options.Ledger ?? throw new InvalidOperationException("REMOTEDEPLOYTOOL_LEDGER_DIR is not set, so there is no ledger to record in.");
        var manifest = DropManifest.Read(Drops.Resolve(options, drop));
        var artifact = manifest.Artifacts.FirstOrDefault(artifact => artifact.Name == name)
            ?? throw new InvalidOperationException($"drop {manifest.Version} holds no {name}.");

        ledger.Record([options.Deployment(target, manifest, artifact, services.Time.GetUtcNow(), error)]);
        output.WriteLine($"Recorded {name} from drop {manifest.Version} as {(error is null ? DeploymentInfo.Succeeded : DeploymentInfo.Failed)} on {target}.");
        return 0;
    }

    private static string Describe(DropManifest manifest) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{manifest.Version}  {(manifest.Sha is { } sha ? sha[..Math.Min(sha.Length, 7)] : "no commit")}  {manifest.Artifacts.Count} artifact(s)");

    private static string Label(string name, string? version) => version is null ? name : $"{name} {version}";

    private static int Version(PipelineOutput output)
    {
        output.WriteLine(ProductVersion.Of(typeof(CommandLine).Assembly));
        return 0;
    }

    private static int Fail(PipelineOutput output, string message)
    {
        output.WriteFailure(message);
        return 1;
    }

    private static int Usage(PipelineOutput output, int exitCode)
    {
        output.WriteLine($"""
            Bitenovac RemoteDeployTool {ProductVersion.Of(typeof(CommandLine).Assembly)}

            Usage:
              remotedeploytool drops                  List every drop, newest first
              remotedeploytool show [drop]            What a drop holds; the newest when none is named
              remotedeploytool changes <from> [to]    What differs from one drop to another, the newest by default
              remotedeploytool nuget <package>... [--drop <drop>] [--same-version]
                                                      Push a drop's packages, by id or by a prefix such as
                                                      Bitenovac.DecompressionCalculator.*; the newest drop by default
              remotedeploytool pages <app> <site> [--drop <drop>]
                                                      Write a drop's web app into an empty directory as a GitHub
                                                      Pages site; the newest drop by default
              remotedeploytool pages <app> <site> --from <target>
                                                      The same, from the drop the ledger says last went through
                                                      to a target, such as staging: a promote
              remotedeploytool cloudflare <app> --project <name> [--branch <branch>] [--drop <drop>]
                                                      Deploy a drop's web app to a Cloudflare Pages project with
                                                      Wrangler; branch main and the newest drop by default
              remotedeploytool status                 Where each artifact stands on each target, from the ledger
              remotedeploytool record <target> <artifact> [--drop <drop>] [--failed <reason>]
                                                      Record a deployment made by something else, such as a
                                                      workflow publishing a Pages site; the newest drop by default
              remotedeploytool --version              Print the version
              remotedeploytool --help                 Print this help

            Environment:
              REMOTEDEPLOYTOOL_DROP_ROOT      Where the drops are (default: artifacts/official)
              REMOTEDEPLOYTOOL_NUGET_SOURCE   Where packages are pushed (default: nuget.org)
              NUGET_API_KEY                   The feed's key; required for a feed on the web
              REMOTEDEPLOYTOOL_LEDGER_DIR     Where the ledger of deployments is kept (default: none kept)
              REMOTEDEPLOYTOOL_MACHINE        The machine the ledger names (default: this one's name)
              CLOUDFLARE_API_TOKEN            A token that may edit Cloudflare Pages; required by cloudflare
              CLOUDFLARE_ACCOUNT_ID           The Cloudflare account; required by cloudflare
            """);
        return exitCode;
    }
}
