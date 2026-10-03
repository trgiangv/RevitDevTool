using System.IO.Pipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Transport;

/// <summary>One connected named pipe served by an SDK <see cref="McpServer"/>.</summary>
public sealed class PipeEndpoint : IAsyncDisposable
{
    private readonly NamedPipeServerStream _pipe;
    private readonly StreamServerTransport _transport;
    private readonly McpServer _server;

    private PipeEndpoint(NamedPipeServerStream pipe, StreamServerTransport transport, McpServer server)
    {
        _pipe = pipe;
        _transport = transport;
        _server = server;
    }

    public McpServer Server => _server;

    public static PipeEndpoint Create(
        NamedPipeServerStream pipe,
        McpServerOptions options,
        IServiceProvider services,
        string transportName)
    {
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var transport = new StreamServerTransport(pipe, pipe, transportName, loggerFactory);
        var server = McpServer.Create(transport, options, loggerFactory, services);
        return new PipeEndpoint(pipe, transport, server);
    }

    public Task RunAsync(CancellationToken cancellationToken) =>
        _server.RunAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        try { await _server.DisposeAsync().ConfigureAwait(false); }
        catch { /* ignored */ }

        try { await _transport.DisposeAsync().ConfigureAwait(false); }
        catch { /* ignored */ }

        try { await _pipe.DisposeAsync().ConfigureAwait(false); }
        catch { /* ignored */ }
    }
}
