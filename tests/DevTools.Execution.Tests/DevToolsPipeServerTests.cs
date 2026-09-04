using System.IO.Pipes;
using System.Text.Json;
using DevTools.Execution.External;
using DevTools.Execution.External.Handlers;
using DevTools.Execution.External.Mcp.Connections;
using DevTools.Hosting;
using DevTools.Ipc;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests;

public sealed class DevToolsPipeServerTests
{
    [Fact]
    public async Task StartAsync_StopAsync_and_Dispose_complete_cleanly()
    {
        using var cts = CreateTimeoutCts();
        var hostInfo = CreateUniqueHostInfo();
        var server = CreateServer(hostInfo, new InstanceRequestHandler(hostInfo));

        await server.StartAsync(cts.Token);
        await server.StopAsync(cts.Token);
        server.Dispose();
    }

    [Fact]
    public async Task InstanceInfo_roundTrips_over_named_pipe()
    {
        using var cts = CreateTimeoutCts();
        var hostInfo = CreateUniqueHostInfo();
        var pipeName = HostPipeName.FormatTest(hostInfo.Host.ToString(), hostInfo.VersionNumber, Environment.ProcessId);
        using var server = CreateServer(hostInfo, new InstanceRequestHandler(hostInfo));

        await server.StartAsync(cts.Token);
        using var client = await ConnectClientAsync(pipeName, cts.Token);

        var response = await SendRequestAsync(
            client,
            BridgeMessage.Request("1", IpcBridgeMethods.InstanceInfo),
            cts.Token);

        Assert.False(response.IsError);
        Assert.NotNull(response.Result);
        var info = response.Result!.Value.Deserialize<InstanceInfo>();
        Assert.NotNull(info);
        Assert.Equal(hostInfo.Host.ToString(), info!.HostApp);
        Assert.Equal(hostInfo.VersionNumber, info.VersionNumber);
        Assert.Equal(Environment.ProcessId, info.ProcessId);

        await server.StopAsync(cts.Token);
    }

    [Fact]
    public async Task UnknownMethod_returnsMethodNotFound()
    {
        using var cts = CreateTimeoutCts();
        var hostInfo = CreateUniqueHostInfo();
        var pipeName = HostPipeName.FormatTest(hostInfo.Host.ToString(), hostInfo.VersionNumber, Environment.ProcessId);
        using var server = CreateServer(hostInfo, new InstanceRequestHandler(hostInfo));

        await server.StartAsync(cts.Token);
        using var client = await ConnectClientAsync(pipeName, cts.Token);

        var response = await SendRequestAsync(
            client,
            BridgeMessage.Request("9", "tests/unknown"),
            cts.Token);

        Assert.True(response.IsError);
        Assert.Equal(IpcErrorCodes.MethodNotFound, response.ErrorDetail?.Code);
        Assert.Contains("Unknown method", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        await server.StopAsync(cts.Token);
    }

    [Fact]
    public async Task Dispose_after_start_always_completes()
    {
        using var cts = CreateTimeoutCts();
        var hostInfo = CreateUniqueHostInfo();
        var server = CreateServer(hostInfo, new InstanceRequestHandler(hostInfo));

        await server.StartAsync(cts.Token);
        server.Dispose();
    }

    private static CancellationTokenSource CreateTimeoutCts()
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));
        return cts;
    }

    private static StubHostAppInfo CreateUniqueHostInfo() =>
        new(Guid.NewGuid().ToString("N"));

    private static DevToolsPipeServer CreateServer(IHostAppInfo hostInfo, params IBridgeRequestHandler[] handlers) =>
        new(
            new McpConnectState(NullLogger<McpConnectState>.Instance),
            hostInfo,
            handlers,
            NullLogger<DevToolsPipeServer>.Instance);

    private static async Task<BridgePipeConnection> ConnectClientAsync(string pipeName, CancellationToken ct)
    {
        var clientPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
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
                // accept loop may not be listening yet
            }
        }

        clientPipe.Dispose();
        throw new TimeoutException($"Could not connect to pipe '{pipeName}'.");
    }

    private static async Task<BridgeMessage> SendRequestAsync(
        BridgePipeConnection connection,
        BridgeMessage request,
        CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<BridgeMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.MessageReceived += msg => tcs.TrySetResult(msg);
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
}
