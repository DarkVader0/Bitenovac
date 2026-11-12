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
    /// <returns>Zero on success, one on failure.</returns>
    public static int Run(string[] args, PipelineOutput output, Func<string, string?> read)
    {
        var options = DeployOptions.FromEnvironment(read, Directory.GetCurrentDirectory());
        try
        {
            return args switch
            {
                [] => Usage(output, 1),
                ["drops"] => ListDrops(options, output),
                ["show"] => Show(options, null, output),
                ["show", var drop] => Show(options, drop, output),
                ["changes", var from] => Changes(options, from, null, output),
                ["changes", var from, var to] => Changes(options, from, to, output),
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
              remotedeploytool --version              Print the version
              remotedeploytool --help                 Print this help

            Environment:
              REMOTEDEPLOYTOOL_DROP_ROOT   Where the drops are (default: artifacts/official)
            """);
        return exitCode;
    }
}
