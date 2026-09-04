using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using DevTools.Daemon.Auth;
using DevTools.Daemon.Composition;
using DevTools.Daemon.Gateway;
using DevTools.Daemon.Tests.Support;
using DevTools.Mcp.Server.Hosting;
using DevTools.Mcp.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DevTools.Daemon.Tests;

public sealed class GatewayComponentTests
{
    [Theory]
    [InlineData("wss://gateway.example/tunnel", "https://gateway.example")]
    [InlineData("https://gateway.example/tunnel", "https://gateway.example")]
    public void GatewayOptions_HttpBaseUrl_StripsTunnelPath(string url, string expectedBase)
    {
        var options = new GatewayOptions { Url = url };
        Assert.Equal(expectedBase, options.HttpBaseUrl);
    }

    [Fact]
    public void TunnelStatusChangedArgs_ExposesStatus()
    {
        var args = new TunnelStatusChangedArgs(TunnelStatus.Connected);
        Assert.Equal(TunnelStatus.Connected, args.Status);
    }

    [Fact]
    public async Task GatewayTunnelClient_ReconnectsUntilCancelled()
    {
        using var host = ServerHostBuilder.CreateStdioHostForTests();
        var engine = host.Services.GetRequiredService<McpEngine>();
        var scanner = DaemonTestDoubles.CreatePipeScanner();
        var options = McpServerFactory.CreateOptions(
            engine.ToolCollection, engine.PromptCollection, host.Services);

        var client = new GatewayTunnelClient(
            new Uri("ws://127.0.0.1:9/tunnel"),
            () => Task.FromResult<string?>("token"),
            options,
            scanner.Object,
            NullLoggerFactory.Instance,
            host.Services,
            NullLoggerFactory.Instance.CreateLogger("GatewayTunnelClient"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        TunnelStatus? lastStatus = null;
        client.StatusChanged += (_, args) => lastStatus = args.Status;

        await client.RunAsync(cts.Token);

        Assert.True(lastStatus is TunnelStatus.Connecting or TunnelStatus.Reconnecting or TunnelStatus.Disconnected);
        await client.DisposeAsync();
    }

    [Fact]
    public async Task GatewayHostedService_StartsWithoutGatewayUrl()
    {
        using var host = ServerHostBuilder.CreateStdioHostForTests();
        var service = new GatewayHostedService(
            DaemonTestDoubles.CreateAuthService().Object,
            host.Services.GetRequiredService<McpEngine>(),
            host.Services.GetRequiredService<IMcpPipeScanner>(),
            Options.Create(new GatewayOptions()),
            NullLoggerFactory.Instance,
            host.Services,
            NullLogger<GatewayHostedService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await service.StartAsync(cts.Token);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(TunnelStatus.Disconnected, service.Status);
    }

    [Fact]
    public async Task WebSocketNdjsonStreams_ReadsLargePayloadAcrossBuffers()
    {
        var port = GetFreeTcpPort();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var serverTask = RunEchoWebSocketServerAsync(port, cts.Token, async (ws, ct) =>
        {
            var payload = Encoding.UTF8.GetBytes(new string('a', 400));
            await ws.SendAsync(payload, WebSocketMessageType.Text, true, ct);
        });

        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/"), cts.Token);

        var readStream = new WebSocketReadStream(ws, 1024 * 1024);
        var first = new byte[128];
        var second = new byte[400];
        Assert.True(await readStream.ReadAsync(first, cts.Token) > 0);
        Assert.True(await readStream.ReadAsync(second, cts.Token) > 0);

        await cts.CancelAsync();
        try { await serverTask; } catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task WebSocketWriteStream_FlushesMultipleLines()
    {
        var port = GetFreeTcpPort();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var serverTask = RunEchoWebSocketServerAsync(port, cts.Token);

        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/"), cts.Token);
        var writeStream = new WebSocketWriteStream(ws, NullLoggerFactory.Instance.CreateLogger("ws"));
        await writeStream.WriteAsync(Encoding.UTF8.GetBytes("{\"a\":1}\n{\"b\":2}\n"), cts.Token);
        await writeStream.FlushAsync(cts.Token);

        await cts.CancelAsync();
        try { await serverTask; } catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task GatewayTunnelClient_ConnectsToLocalServer_BeforeCancellation()
    {
        var port = GetFreeTcpPort();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var serverTask = RunEchoWebSocketServerAsync(port, cts.Token);

        using var host = ServerHostBuilder.CreateStdioHostForTests();
        var engine = host.Services.GetRequiredService<McpEngine>();
        var scanner = DaemonTestDoubles.CreatePipeScanner();
        var options = McpServerFactory.CreateOptions(
            engine.ToolCollection, engine.PromptCollection, host.Services);

        var client = new GatewayTunnelClient(
            new Uri($"ws://127.0.0.1:{port}/"),
            () => Task.FromResult<string?>("token"),
            options,
            scanner.Object,
            NullLoggerFactory.Instance,
            host.Services,
            NullLogger<GatewayTunnelClient>.Instance);

        await client.RunAsync(cts.Token);
        await client.DisposeAsync();

        await cts.CancelAsync();
        try { await serverTask; } catch (OperationCanceledException) { }
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task RunEchoWebSocketServerAsync(
        int port,
        CancellationToken ct,
        Func<WebSocket, CancellationToken, Task>? onConnected = null)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        while (!ct.IsCancellationRequested)
        {
            var context = await listener.GetContextAsync().WaitAsync(ct);
            if (!context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
                continue;
            }

            var wsContext = await context.AcceptWebSocketAsync(null);
            _ = Task.Run(async () =>
            {
                if (onConnected is not null)
                {
                    await onConnected(wsContext.WebSocket, ct);
                    return;
                }

                var buffer = new byte[4096];
                while (wsContext.WebSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var result = await wsContext.WebSocket.ReceiveAsync(buffer, ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    await wsContext.WebSocket.SendAsync(
                        buffer.AsMemory(0, result.Count),
                        WebSocketMessageType.Text,
                        result.EndOfMessage,
                        ct);
                }
            }, ct);
        }
    }
}
