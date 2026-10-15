using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Represents the socket <c>remotebuildtool wake</c> connects to; every connection asks the
/// promotion work for a turn now.
/// </summary>
/// <remarks>
/// The workflow run on each merge into the branch connects through <c>docker exec</c>, so a merge
/// reaches main within seconds while the promotion work's own wait is only a fallback.
/// </remarks>
/// <param name="host">The machine's settings.</param>
/// <param name="wake">The requests the promotion work waits for.</param>
/// <param name="logger">Where the listener reports.</param>
internal sealed class WakeListener(HostSettings host, ServiceWake wake, ILogger<WakeListener> logger) : BackgroundService
{
    private readonly Socket _socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

    /// <summary>
    /// Opens the socket, so a wake made once the service has started is never refused.
    /// </summary>
    /// <param name="cancellationToken">Stops the start.</param>
    /// <returns>A task that completes once the listener runs.</returns>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        var path = ServiceWake.SocketPath(host);
        Directory.CreateDirectory(host.WorkRoot);
        // A socket left by an earlier container would make the bind fail.
        File.Delete(path);
        _socket.Bind(new UnixDomainSocketEndPoint(path));
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
                logger.LogInformation("{Message}", "woken: a pull request was merged");
                wake.Request();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
