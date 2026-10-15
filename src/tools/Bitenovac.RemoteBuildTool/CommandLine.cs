using Bitenovac.RemoteBuildTool.Commands;
using Bitenovac.RemoteBuildTool.Hashing;

namespace Bitenovac.RemoteBuildTool;

/// <summary>
/// Provides methods for dispatching command-line arguments to a pipeline command.
/// </summary>
internal static class CommandLine
{
    public static int Run(string[] args, PipelineOutput output)
    {
        if (args.Length == 0)
        {
            PrintUsage(output);
            return 1;
        }

        var options = PipelineOptions.FromEnvironment();
        try
        {
            return args[0] switch
            {
                "plan" => PlanCommand.Run(options, output),
                "build" => WithConfiguration(args, output, configuration => BuildCommand.Run(options, configuration, output)),
                "test" => WithConfiguration(args, output, configuration => TestCommand.Run(options, configuration, output)),
                "promote" => PromoteCommand.Run(options, output),
                "release" => ReleaseCommand.Run(options, output),
                "graph" => GraphCommand.Run(options, output),
                "serve" => ServeCommand.Run(output, SettingsPath(Environment.GetEnvironmentVariable)),
                "wake" => WakeCommand.Run(output, SettingsPath(Environment.GetEnvironmentVariable)),
                "--help" or "-h" => Usage(output),
                "--version" => Version(output),
                _ => Fail(output, $"Unknown command '{args[0]}'."),
            };
        }
        catch (InvalidOperationException exception)
        {
            return Fail(output, exception.Message);
        }
    }

    /// <summary>
    /// Returns the service's host settings file: <c>REMOTEBUILDTOOL_CONFIG</c>, or the default when unset.
    /// </summary>
    /// <param name="read">Returns an environment variable's value, or <see langword="null"/> when it is unset.</param>
    /// <returns>The settings file.</returns>
    public static string SettingsPath(Func<string, string?> read) =>
        read("REMOTEBUILDTOOL_CONFIG") is { Length: > 0 } settings ? settings : ServeCommand.DefaultSettingsPath;

    private static int WithConfiguration(string[] args, PipelineOutput output, Func<string, int> run)
    {
        if (args.Length < 2)
            return Fail(output, "A configuration is required: 'Debug' or 'Release'.");

        return args[1] switch
        {
            "Debug" or "Release" => run(args[1]),
            _ => Fail(output, $"Configuration must be 'Debug' or 'Release', got '{args[1]}'."),
        };
    }

    private static int Version(PipelineOutput output)
    {
        output.WriteLine(ToolVersion.Value);
        return 0;
    }

    private static int Usage(PipelineOutput output)
    {
        PrintUsage(output);
        return 0;
    }

    private static int Fail(PipelineOutput output, string message)
    {
        output.WriteFailure(message);
        return 1;
    }

    private static void PrintUsage(PipelineOutput output) =>
        output.WriteLine($"""
            Bitenovac RemoteBuildTool {ToolVersion.Value}

            Usage:
              remotebuildtool plan              Discover, verify, restore and hash every project; decide what main can serve
              remotebuildtool build <Config>    Restore hits from main, build misses, stage them in this run's store
              remotebuildtool test <Config>     Run or reuse each test project; on Debug, gate coverage
              remotebuildtool promote           Copy this run's entries into main
              remotebuildtool release           Build every project but the tests in Release under the next release
                                                version and drop what can be deployed
              remotebuildtool graph             Print the project graph with each project's hashes
              remotebuildtool serve             Run the service: promote merged pull requests into main, record
                                                builds, compact telemetry, prune logs, rebuild main nightly
              remotebuildtool wake              Ask the running service to promote merged pull requests now
              remotebuildtool --version         Print the version
              remotebuildtool --help            Print this help

            Environment:
              REMOTEBUILDTOOL_REPO_ROOT       Repository root (default: current directory)
              REMOTEBUILDTOOL_MAIN_STORE      The shared cache, main (default: artifacts/ci-main)
              REMOTEBUILDTOOL_PR_STORE        This run's own store, which holds its plan (default: artifacts/ci-pr)
              REMOTEBUILDTOOL_DROP_ROOT       Where 'release' drops each version (default: artifacts/official)
              REMOTEBUILDTOOL_CACHELESS       true or 1 to ignore main and rebuild everything
              REMOTEBUILDTOOL_COVERAGE_HTML   0 to skip the HTML coverage report (default: written)
              REMOTEBUILDTOOL_MAX_CPU         Processes and MSBuild nodes a command runs at once (default: every CPU)
              REMOTEBUILDTOOL_LOGS_DIR        Where binary logs and the build graph go (default: none kept)
              REMOTEBUILDTOOL_TELEMETRY_DIR   Where every command writes its telemetry (default: none written)
              REMOTEBUILDTOOL_CONFIG          The service's host settings (default: {ServeCommand.DefaultSettingsPath})
            """);
}
