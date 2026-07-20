using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Hosting;

/// <summary>Registry of independent daemon SDK servers keyed by gateway logical session ID.</summary>
public sealed class GatewaySessionManager(
    Func<McpServerOptions> optionsFactory,
    ILoggerFactory loggerFactory,
    IServiceProvider services) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, GatewayMcpSession> sessions = new(StringComparer.Ordinal);
    private int disposed;

    public bool Contains(string sessionId) => sessions.ContainsKey(sessionId);
    public int Count => sessions.Count;

    public async Task OpenAsync(
        string sessionId,
        Func<GatewayTunnelEnvelope, CancellationToken, ValueTask> sendAsync,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("A gateway session ID is required.", nameof(sessionId));

        var created = new GatewayMcpSession(sessionId, optionsFactory, sendAsync, loggerFactory, services, cancellationToken);
        if (!sessions.TryAdd(sessionId, created))
        {
            await created.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException($"Gateway session '{sessionId}' is already open.");
        }

        try
        {
            await sendAsync(new GatewayTunnelEnvelope(GatewayTunnelEnvelope.ProtocolVersion, GatewayTunnelEnvelope.SessionOpened, sessionId), cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            sessions.TryRemove(sessionId, out _);
            await created.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public ValueTask<bool> RouteAsync(string sessionId, JsonElement message, CancellationToken cancellationToken) =>
        sessions.TryGetValue(sessionId, out var session)
            ? session.RouteAsync(message, cancellationToken)
            : ValueTask.FromResult(false);

    public async ValueTask CloseAsync(string sessionId, string reason, CancellationToken cancellationToken)
    {
        if (!sessions.TryRemove(sessionId, out var session)) return;
        try { await session.DisposeAsync().ConfigureAwait(false); }
        finally
        {
            await SendClosedAsync(session, reason, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        var snapshot = sessions.ToArray();
        sessions.Clear();
        foreach (var (_, session) in snapshot)
            await session.DisposeAsync().ConfigureAwait(false);
    }

    private static ValueTask SendClosedAsync(GatewayMcpSession session, string reason, CancellationToken cancellationToken) =>
        session.SendAsync(GatewayTunnelEnvelope.Closed(session.SessionId, reason), cancellationToken);

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref disposed) != 0) throw new ObjectDisposedException(nameof(GatewaySessionManager));
    }
}
