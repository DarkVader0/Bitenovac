using System.Runtime.InteropServices;
using Bitenovac.RemoteBuildTool.Service;

namespace Bitenovac.RemoteBuildTool.Commands;

/// <summary>
/// Provides the <c>serve</c> command, which runs the service until it is stopped.
/// </summary>
/// <remarks>
/// The service fills main from merged pull requests, records every build, compacts telemetry,
/// prunes logs and audits the cache; see <see cref="ServiceLoop"/>. It stops on SIGTERM or Ctrl+C,
/// cancelling a running build.
/// </remarks>
internal static class ServeCommand
{
    /// <summary>
    /// Where the host settings are read from unless <c>REMOTEBUILDTOOL_CONFIG</c> names another file.
    /// </summary>
    public const string DefaultSettingsPath = "/etc/remotebuildtool/config.json";

    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Runs the service.
    /// </summary>
    /// <param name="output">Where the service reports what it does.</param>
    /// <param name="settingsPath">The host settings file.</param>
    /// <returns>0 once stopped.</returns>
    /// <exception cref="InvalidOperationException">The host settings are missing or invalid.</exception>
    public static int Run(PipelineOutput output, string settingsPath)
    {
        var host = HostSettings.Load(settingsPath);
        using var http = GitHubClient.CreateHttpClient(host.GitHubApiUrl, host.GitHubToken);
        using var stop = new CancellationTokenSource();
        using var terminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            stop.Cancel();
        });
        using var interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
        {
            context.Cancel = true;
            stop.Cancel();
        });

        var loop = new ServiceLoop(host, new GitHubClient(http, host.Repository), ToolCommand.Self, output, TimeProvider.System);
        output.WriteLine($"Serving {host.Repository} as {host.EffectiveMachine} in pool {host.Pool}.");
        loop.RunAsync(Tick, stop.Token).GetAwaiter().GetResult();
        output.WriteLine("Stopped.");
        return 0;
    }
}
