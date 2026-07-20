using System.Collections.Concurrent;
using System.Text.Json;
using DevTools.Daemon.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;

namespace RevitDevTool.Server.Tests;

public sealed class GatewaySessionManagerTests
{
    [Fact]
    public async Task TwoLogicalSessions_CanBothInitializeWithRequestIdOne()
    {
        var sent = new ConcurrentQueue<GatewayTunnelEnvelope>();
        var created = 0;
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var manager = new GatewaySessionManager(
            () => { Interlocked.Increment(ref created); return new McpServerOptions(); },
            NullLoggerFactory.Instance,
            services);

        await manager.OpenAsync("gw_a", SendAsync, TestContext.Current.CancellationToken);
        await manager.OpenAsync("gw_b", SendAsync, TestContext.Current.CancellationToken);
        await Task.WhenAll(
            manager.RouteAsync("gw_a", InitializeMessage(1), TestContext.Current.CancellationToken).AsTask(),
            manager.RouteAsync("gw_b", InitializeMessage(1), TestContext.Current.CancellationToken).AsTask());

        await WaitForAsync(() => sent.Count(envelope => envelope.Type == GatewayTunnelEnvelope.McpMessage) == 2);
        Assert.Equal(2, created);
        Assert.Equal(["gw_a", "gw_b"], sent.Where(envelope => envelope.Type == GatewayTunnelEnvelope.McpMessage).Select(envelope => envelope.SessionId).Order());

        ValueTask SendAsync(GatewayTunnelEnvelope envelope, CancellationToken _) { sent.Enqueue(envelope); return ValueTask.CompletedTask; }
    }

    [Fact]
    public async Task ClosingOneSession_DoesNotCancelAnother()
    {
        var sent = new ConcurrentQueue<GatewayTunnelEnvelope>();
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var manager = new GatewaySessionManager(() => new McpServerOptions(), NullLoggerFactory.Instance, services);
        ValueTask SendAsync(GatewayTunnelEnvelope envelope, CancellationToken _) { sent.Enqueue(envelope); return ValueTask.CompletedTask; }
        await manager.OpenAsync("gw_a", SendAsync, TestContext.Current.CancellationToken);
        await manager.OpenAsync("gw_b", SendAsync, TestContext.Current.CancellationToken);

        await manager.CloseAsync("gw_a", "client_delete", TestContext.Current.CancellationToken);

        Assert.False(manager.Contains("gw_a"));
        Assert.True(manager.Contains("gw_b"));
        Assert.True(await manager.RouteAsync("gw_b", PingMessage(2), TestContext.Current.CancellationToken));
        Assert.Contains(sent, envelope => envelope.Type == GatewayTunnelEnvelope.SessionClosed && envelope.SessionId == "gw_a" && envelope.Reason == "client_delete");
    }

    [Fact]
    public async Task UnknownRoute_IsRejectedForCarrierToCloseWithStableReason()
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var manager = new GatewaySessionManager(() => new McpServerOptions(), NullLoggerFactory.Instance, services);

        Assert.False(await manager.RouteAsync("gw_missing", PingMessage(1), TestContext.Current.CancellationToken));
        Assert.Equal(GatewayTunnelEnvelope.UnknownSession, GatewayTunnelEnvelope.Closed("gw_missing", GatewayTunnelEnvelope.UnknownSession).Reason);
    }

    private static JsonElement InitializeMessage(int id) => JsonSerializer.SerializeToElement(new
    {
        jsonrpc = "2.0", id, method = "initialize",
        @params = new { protocolVersion = "2025-03-26", capabilities = new { }, clientInfo = new { name = "test", version = "1.0" } }
    });

    private static JsonElement PingMessage(int id) => JsonSerializer.SerializeToElement(new { jsonrpc = "2.0", id, method = "ping", @params = new { } });

    private static async Task WaitForAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(10, timeout.Token);
    }
}
