using System.Net;
using System.Net.Sockets;

namespace DevTools.Daemon.Tests.Support;

internal sealed class RejectingServer : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

    public RejectingServer()
    {
        _listener.Start();
        _ = Task.Run(AcceptAndCloseAsync);
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public void Dispose() => _listener.Stop();

    private async Task AcceptAndCloseAsync()
    {
        try
        {
            while (true)
            {
                using var client = await _listener.AcceptTcpClientAsync();
                client.Client.LingerState = new LingerOption(true, 0);
            }
        }
        catch (Exception ex) when (ex is ObjectDisposedException or SocketException)
        {
        }
    }
}
