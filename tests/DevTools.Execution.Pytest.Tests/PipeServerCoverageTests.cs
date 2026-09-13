using System.Text.Json;
using DevTools.Execution.External;
using DevTools.Execution.External.Handlers;
using DevTools.Execution.External.Mcp.Connections;
using DevTools.Hosting;
using DevTools.Ipc;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class PipeServerCoverageTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task DevToolsPipeServer_StartAsync_WiresNotificationPublisher()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        var hostInfo = new StubHostAppInfo(Guid.NewGuid().ToString("N"));
        var handler = new NotificationPingHandler();
        using var server = new DevToolsPipeServer(
            new McpConnectState(NullLogger<McpConnectState>.Instance),
            hostInfo,
            [handler, new InstanceRequestHandler(hostInfo)],
            NullLogger<DevToolsPipeServer>.Instance);

        await server.StartAsync(cts.Token);
        var pipeName = HostPipeName.FormatTest(hostInfo.Host.ToString(), hostInfo.VersionNumber, Environment.ProcessId);

        using var client = await ConnectClientAsync(pipeName, cts.Token);
        var notificationTcs = new TaskCompletionSource<BridgeMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.MessageReceived += msg =>
        {
            if (msg.Type == BridgeMessage.TypeNotification)
                notificationTcs.TrySetResult(msg);
        };

        var response = await SendRequestAsync(
            client,
            BridgeMessage.Request("notify-1", NotificationPingHandler.PingMethod),
            cts.Token);

        Assert.IsFalse(response.IsError);
        var notification = await notificationTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);
        Assert.AreEqual(NotificationPingHandler.ProgressMethod, notification.Method);

        await server.StopAsync(cts.Token);
    }

    private static async Task<BridgePipeConnection> ConnectClientAsync(string pipeName, CancellationToken ct)
    {
        var clientPipe = new System.IO.Pipes.NamedPipeClientStream(".", pipeName, System.IO.Pipes.PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous);
        for (var attempt = 0; attempt < 50; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await clientPipe.ConnectAsync(50, ct).ConfigureAwait(false);
                return new BridgePipeConnection(clientPipe);
            }
            catch (TimeoutException) when (attempt < 49)
            {
            }
        }

        clientPipe.Dispose();
        throw new TimeoutException($"Could not connect to pipe '{pipeName}'.");
    }

    private static async Task<BridgeMessage> SendRequestAsync(BridgePipeConnection connection, BridgeMessage request, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<BridgeMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.MessageReceived += msg =>
        {
            if (msg.Type == BridgeMessage.TypeResponse)
                tcs.TrySetResult(msg);
        };
        connection.StartReadLoop();

        await connection.WriteAsync(request, ct).ConfigureAwait(false);
        return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
    }

    private sealed class StubHostAppInfo(string version) : IHostAppInfo
    {
        public HostApp Host => HostApp.Revit;
        public string VersionNumber { get; } = version;
        public string? VersionBuild => null;
        public int ProcessId => Environment.ProcessId;
    }

    private sealed class NotificationPingHandler : IBridgeRequestHandler, IBridgeNotificationPublisher
    {
        public const string PingMethod = "tests/ping";
        public const string ProgressMethod = "notifications/tests/progress";

        public IReadOnlyCollection<string> SupportedMethods => [PingMethod];

        public Action<string, JsonElement?>? NotificationSender { get; set; }

        public Task<BridgeMessage> HandleAsync(string id, string method, JsonElement? parameters, CancellationToken cancellationToken)
        {
            NotificationSender?.Invoke(ProgressMethod, null);
            return Task.FromResult(BridgeMessage.Response(id, JsonSerializer.SerializeToElement(new { ok = true })));
        }
    }
}
