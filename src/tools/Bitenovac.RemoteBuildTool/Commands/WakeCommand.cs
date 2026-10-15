using System.Net.Sockets;
using Bitenovac.RemoteBuildTool.Service;

namespace Bitenovac.RemoteBuildTool.Commands;

/// <summary>
/// Provides the <c>wake</c> command, which asks the running service to promote merged pull requests
/// now rather than at its next poll.
/// </summary>
/// <remarks>
/// The workflow run on each merge calls it inside the service's container; see
/// <see cref="WakeListener"/>.
/// </remarks>
internal static class WakeCommand
{
    /// <summary>
    /// Wakes the service.
    /// </summary>
    /// <param name="output">Where to report.</param>
    /// <param name="settingsPath">The host settings file, which names the service's work directory.</param>
    /// <returns>0 once the service was asked; 1 when it is not listening.</returns>
    /// <exception cref="InvalidOperationException">The host settings are missing or invalid.</exception>
    public static int Run(PipelineOutput output, string settingsPath)
    {
        var path = ServiceWake.SocketPath(HostSettings.Load(settingsPath));
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            socket.Connect(new UnixDomainSocketEndPoint(path));
        }
        catch (SocketException exception)
        {
            output.WriteFailure($"the service is not listening at {path}: {exception.Message}");
            return 1;
        }

        output.WriteLine("Woke the service.");
        return 0;
    }
}
