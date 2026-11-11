using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bitenovac.RemoteTools.Hosting;

/// <summary>
/// Represents the socket a service's <c>wake</c> command connects to; every connection asks the
/// waiting work for a turn now.
/// </summary>
/// <remarks>
/// A workflow connects through <c>docker exec</c> when something happened, so the service acts
/// within seconds while the work's own wait is only a fallback.
/// </remarks>
/// <param name="socketPath">The file of the socket, from <see cref="ServiceWake.SocketPath"/>.</param>
/// <param name="wokenMessage">What the listener reports on every connection.</param>
/// <param name="wake">The requests the work waits for.</param>
/// <param name="logger">Where the listener reports.</param>
internal sealed class WakeListener(string socketPath, string wokenMessage, ServiceWake wake, ILogger<WakeListener> logger) : BackgroundService
{
    private static readonly Action<ILogger, string, Exception?> Report =
        LoggerMessage.Define<string>(LogLevel.Information, default, "{Message}");

    private readonly Socket _socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

    /// <summary>
    /// Opens the socket, so a wake made once the service has started is never refused.
    /// </summary>
    /// <param name="cancellationToken">Stops the start.</param>
    /// <returns>A task that completes once the listener runs.</returns>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(socketPath)!);
        // A socket left by an earlier container would make the bind fail.
        File.Delete(socketPath);
        _socket.Bind(new UnixDomainSocketEndPoint(socketPath));
        _socket.Listen();
        return base.StartAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        _socket.Dispose();
        base.Dispose();
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (true)
            {
                using var client = await _socket.AcceptAsync(stoppingToken);
                Report(logger, wokenMessage, null);
                wake.Request();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
