using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Client.Tests;

public class NamedPipeSdkIntegrationTests
{
    [Fact]
    public async Task NamedPipe_DaemonClientTalksToHostSdkServer()
    {
        var pipeName = HostPipeName.FormatMcp("TestHost", Guid.NewGuid().ToString("N")[..8], Environment.ProcessId);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(20));

        var tools = new McpServerPrimitiveCollection<McpServerTool>
        {
            McpServerTool.Create(
                () => "pong",
                new McpServerToolCreateOptions { Name = "ping", Description = "Ping" })
        };

        var options = new McpServerOptions
        {
            ServerInfo = new Implementation { Name = "named-pipe-host", Version = "1.0.0" },
            ToolCollection = tools,
            Capabilities = new ServerCapabilities
            {
                Tools = new ToolsCapability { ListChanged = true }
            }
        };

        await using var serverPipe = CreateServerPipe(pipeName);
        var acceptTask = serverPipe.WaitForConnectionAsync(cts.Token);

        await using var clientPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await clientPipe.ConnectAsync(cts.Token);
        await acceptTask;

        var transport = new StreamServerTransport(serverPipe, serverPipe, "named-pipe-server", NullLoggerFactory.Instance);
        using var appServices = TestMcpAppServices.Create();
        await using var server = McpServer.Create(transport, options, NullLoggerFactory.Instance, appServices);
        var serverTask = server.RunAsync(cts.Token);

        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(clientPipe, clientPipe, NullLoggerFactory.Instance),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: cts.Token);

        var listed = await client.ListToolsAsync(cancellationToken: cts.Token);
        Assert.Contains(listed, t => t.Name == "ping");

        var result = await client.CallToolAsync("ping", cancellationToken: cts.Token);
        Assert.NotEqual(true, result.IsError);
        Assert.Contains("pong", result.Content.OfType<TextContentBlock>().Select(c => c.Text));

        await client.DisposeAsync();
        await cts.CancelAsync();
        try { await serverTask; } catch { /* ignored */ }
    }

    private static NamedPipeServerStream CreateServerPipe(string pipeName)
    {
        var security = new PipeSecurity();
        var currentUser = WindowsIdentity.GetCurrent();
        Assert.NotNull(currentUser.User);
        security.AddAccessRule(new PipeAccessRule(
            currentUser.User,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            security);
    }
}
