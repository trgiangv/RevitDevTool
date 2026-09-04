using System.IO.Pipes;
using DevTools.Execution.External;
using DevTools.Execution.External.Handlers;
using DevTools.Execution.External.Mcp.Connections;
using DevTools.Hosting;
using DevTools.Ipc;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests;

public sealed class DevToolsPipeServerDisconnectTests
{
    [Fact]
    public async Task ClientDisconnect_AllowsServerStop()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        var hostInfo = new StubHostAppInfo(Guid.NewGuid().ToString("N"));
        using var server = new DevToolsPipeServer(
            new McpConnectState(NullLogger<McpConnectState>.Instance),
            hostInfo,
            [new InstanceRequestHandler(hostInfo)],
            NullLogger<DevToolsPipeServer>.Instance);

        await server.StartAsync(cts.Token);
        var pipeName = HostPipeName.FormatTest(hostInfo.Host.ToString(), hostInfo.VersionNumber, Environment.ProcessId);

        var clientPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await clientPipe.ConnectAsync(5000, cts.Token);
        clientPipe.Dispose();

        await Task.Delay(500, cts.Token);
        await server.StopAsync(cts.Token);
    }

    private sealed class StubHostAppInfo(string version) : IHostAppInfo
    {
        public HostApp Host => HostApp.Revit;
        public string VersionNumber { get; } = version;
        public string? VersionBuild => null;
        public int ProcessId => Environment.ProcessId;
    }
}
