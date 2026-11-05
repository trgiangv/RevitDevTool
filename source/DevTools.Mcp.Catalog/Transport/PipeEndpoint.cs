using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.IO.Pipes;

namespace DevTools.Mcp.Transport;

/// <summary>One connected named pipe served by an SDK <see cref="McpServer"/>.</summary>
public sealed class PipeEndpoint : IAsyncDisposable
{
    private readonly NamedPipeServerStream _pipe;
    private readonly StreamServerTransport _transport;
    private readonly McpServer _server;
    private readonly CancellationTokenSource _session;

    private PipeEndpoint(
        NamedPipeServerStream pipe,
        StreamServerTransport transport,
        McpServer server,
        CancellationTokenSource session)
    {
        _pipe = pipe;
        _transport = transport;
        _server = server;
        _session = session;
    }

    public McpServer Server => _server;

    public bool IsClientConnected
    {
        get
        {
            try
            {
                return _pipe.IsConnected;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }
    }

    public static PipeEndpoint Create(
        NamedPipeServerStream pipe,
        McpServerOptions options,
        IServiceProvider services,
        string transportName)
    {
        var session = new CancellationTokenSource();
        var loggerFactory = new SessionLoggerFactory(services.GetRequiredService<ILoggerFactory>(), session.Token);
        var transport = new StreamServerTransport(pipe, pipe, transportName, loggerFactory);
        var server = McpServer.Create(transport, options, loggerFactory, services);
        return new PipeEndpoint(pipe, transport, server, session);
    }

    /// <summary>
    ///     Runs the session until the host stops or the client goes. When the client goes, it cancels the requests that
    ///     stay open (<c>subscriptions/listen</c>), so the pipe instance is free again.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var hostStopping = cancellationToken.Register(_session.Cancel);
        var run = _server.RunAsync(_session.Token);
        await Task.WhenAny(run, _transport.MessageReader.Completion).ConfigureAwait(false);
        _session.Cancel();
        try
        {
            await run.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        try { await _server.DisposeAsync().ConfigureAwait(false); }
        catch { /* ignored */ }

        try { await _transport.DisposeAsync().ConfigureAwait(false); }
        catch { /* ignored */ }

        try { await _pipe.DisposeAsync().ConfigureAwait(false); }
        catch { /* ignored */ }

        _session.Dispose();
    }
}
