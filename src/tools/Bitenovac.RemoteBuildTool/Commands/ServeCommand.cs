using Bitenovac.RemoteBuildTool.Service;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Bitenovac.RemoteBuildTool.Commands;

/// <summary>
/// Provides the <c>serve</c> command, which runs the service until it is stopped.
/// </summary>
/// <remarks>
/// The service's work runs as hosted services; see <see cref="ServiceComposition"/>. It stops on
/// SIGTERM or Ctrl+C, cancelling a running rebuild.
/// </remarks>
internal static class ServeCommand
{
    /// <summary>
    /// Where the host settings are read from unless <c>REMOTEBUILDTOOL_CONFIG</c> names another file.
    /// </summary>
    public const string DefaultSettingsPath = "/etc/remotebuildtool/config.json";

    /// <summary>
    /// Runs the service.
    /// </summary>
    /// <param name="output">Where the service reports starting and stopping.</param>
    /// <param name="settingsPath">The host settings file.</param>
    /// <returns>0 once stopped.</returns>
    /// <exception cref="InvalidOperationException">The host settings are missing or invalid.</exception>
    public static int Run(PipelineOutput output, string settingsPath)
    {
        var host = HostSettings.Load(settingsPath);
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Logging.AddSimpleConsole(console =>
        {
            console.SingleLine = true;
            console.UseUtcTimestamp = true;
            console.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
            console.ColorBehavior = LoggerColorBehavior.Disabled;
        });
        builder.Services.AddRemoteBuildToolService(host, ToolCommand.Self, TimeProvider.System);

        using var service = builder.Build();
        output.WriteLine($"Serving {host.Repository} as {host.EffectiveMachine} in pool {host.Pool}.");
        service.Run();
        output.WriteLine("Stopped.");
        return 0;
    }
}
