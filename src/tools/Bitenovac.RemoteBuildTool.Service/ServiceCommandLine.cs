using Bitenovac.RemoteBuildTool.Commands;
using Bitenovac.RemoteBuildTool.Hashing;

namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Provides methods for dispatching the service's command-line arguments: <c>serve</c> and
/// <c>wake</c> here, every other command to <see cref="CommandLine"/>.
/// </summary>
/// <remarks>
/// The service runs the pipeline commands as this same program, so it answers all of them.
/// </remarks>
internal static class ServiceCommandLine
{
    public static int Run(string[] args, PipelineOutput output)
    {
        try
        {
            return args switch
            {
                [] => Usage(output, 1),
                ["serve", ..] => ServeCommand.Run(output, SettingsPath(Environment.GetEnvironmentVariable)),
                ["wake", ..] => WakeCommand.Run(output, SettingsPath(Environment.GetEnvironmentVariable)),
                ["--help" or "-h", ..] => Usage(output, 0),
                _ => CommandLine.Run(args, output),
            };
        }
        catch (InvalidOperationException exception)
        {
            output.WriteFailure(exception.Message);
            return 1;
        }
    }

    /// <summary>
    /// Returns the service's host settings file: <c>REMOTEBUILDTOOL_CONFIG</c>, or the default when unset.
    /// </summary>
    /// <param name="read">Returns an environment variable's value, or <see langword="null"/> when it is unset.</param>
    /// <returns>The settings file.</returns>
    public static string SettingsPath(Func<string, string?> read) =>
        read("REMOTEBUILDTOOL_CONFIG") is { Length: > 0 } settings ? settings : ServeCommand.DefaultSettingsPath;

    private static int Usage(PipelineOutput output, int exitCode)
    {
        output.WriteLine($"""
            Bitenovac RemoteBuildTool service {ToolVersion.Value}

            Usage:
              remotebuildtool-service serve       Run the service: promote merged pull requests into main, record
                                                  builds, compact telemetry, prune logs, rebuild main nightly
              remotebuildtool-service wake        Ask the running service to promote merged pull requests now
              remotebuildtool-service <command>   Run any RemoteBuildTool command below, such as plan

            Environment:
              REMOTEBUILDTOOL_CONFIG   The service's host settings (default: {ServeCommand.DefaultSettingsPath})

            """);
        CommandLine.Run(["--help"], output);
        return exitCode;
    }
}
