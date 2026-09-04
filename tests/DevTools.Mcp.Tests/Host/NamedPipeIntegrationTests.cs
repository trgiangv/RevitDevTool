using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json.Nodes;
using DevTools.Mcp.Adapter.Host;
using DevTools.Mcp.Tests.Harness;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Tests.Host;

public sealed class NamedPipeIntegrationTests
{
    [Fact]
    public async Task NamedPipe_McpClientTalksToHostHandler()
    {
        var pipeName = HostPipeName.FormatMcp("TestHost", Guid.NewGuid().ToString("N")[..8], Environment.ProcessId);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(20));

        var (handler, _) = McpHostTestHarness.CreateWithTool("ping", "pong", "Ping");

        await using var serverPipe = CreateServerPipe(pipeName);
        var acceptTask = serverPipe.WaitForConnectionAsync(cts.Token);

        await using var clientPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await clientPipe.ConnectAsync(cts.Token);
        await acceptTask;

        await using var session = McpPipeSession.Start(serverPipe, handler, cts.Token);

        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(clientPipe, clientPipe, NullLoggerFactory.Instance),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: cts.Token);

        var listed = await client.ListToolsAsync(cancellationToken: cts.Token);
        Assert.Contains(listed, tool => tool.Name == "ping");

        var result = await client.CallToolAsync("ping", cancellationToken: cts.Token);
        Assert.NotEqual(true, result.IsError);
        Assert.Contains("pong", result.Content.OfType<TextContentBlock>().Select(block => block.Text));

        await client.DisposeAsync();
        await cts.CancelAsync();
    }

    [Fact]
    public async Task NamedPipe_ClientReceivesToolListChangedNotification()
    {
        var pipeName = HostPipeName.FormatMcp("TestHost", Guid.NewGuid().ToString("N")[..8], Environment.ProcessId);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(20));

        var (handler, _) = McpHostTestHarness.CreateWithTool("ping", "pong", "Ping");

        await using var serverPipe = CreateServerPipe(pipeName);
        var acceptTask = serverPipe.WaitForConnectionAsync(cts.Token);

        await using var clientPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await clientPipe.ConnectAsync(cts.Token);
        await acceptTask;

        await using var session = McpPipeSession.Start(serverPipe, handler, cts.Token);

        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(clientPipe, clientPipe, NullLoggerFactory.Instance),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: cts.Token);

        var notificationReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var registration = client.RegisterNotificationHandler(
            NotificationMethods.ToolListChangedNotification,
            (_, _) =>
            {
                notificationReceived.TrySetResult();
                return default;
            });

        await session.SendNotificationAsync(NotificationMethods.ToolListChangedNotification, cts.Token);
        await notificationReceived.Task.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);

        await client.DisposeAsync();
        await cts.CancelAsync();
    }

    [Fact]
    public async Task NamedPipe_HandlerException_ReturnsJsonRpcInternalError()
    {
        var pipeName = HostPipeName.FormatMcp("TestHost", Guid.NewGuid().ToString("N")[..8], Environment.ProcessId);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(20));

        await using var serverPipe = CreateServerPipe(pipeName);
        var acceptTask = serverPipe.WaitForConnectionAsync(cts.Token);

        await using var clientPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await clientPipe.ConnectAsync(cts.Token);
        await acceptTask;

        await using var session = McpPipeSession.Start(serverPipe, new ThrowingMcpHandler(), cts.Token);

        var request = """{"jsonrpc":"2.0","id":7,"method":"tools/list"}""" + "\n";
        var bytes = Encoding.UTF8.GetBytes(request);
        await clientPipe.WriteAsync(bytes, cts.Token);
        await clientPipe.FlushAsync(cts.Token);

        using var reader = new StreamReader(
            clientPipe,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);
        var line = await reader.ReadLineAsync(cts.Token);
        Assert.NotNull(line);

        var json = JsonNode.Parse(line)!.AsObject();
        Assert.Equal(7, json["id"]!.GetValue<int>());
        Assert.Equal((int)ModelContextProtocol.McpErrorCode.InternalError, json["error"]!["code"]!.GetValue<int>());
        Assert.Contains("boom", json["error"]!["message"]!.GetValue<string>(), StringComparison.Ordinal);

        await cts.CancelAsync();
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

file sealed class ThrowingMcpHandler : IMcpHandler
{
    public ValueTask<JsonObject?> HandleAsync(JsonObject request, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("boom");
}
