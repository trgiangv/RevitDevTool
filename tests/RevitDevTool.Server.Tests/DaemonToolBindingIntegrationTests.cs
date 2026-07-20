using System.IO.Pipes;
using DevTools.Daemon.Hosting;
using DevTools.Daemon.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace RevitDevTool.Server.Tests;

[Collection(HostMcpServerPipeCollection.Name)]
public sealed class DaemonToolBindingIntegrationTests
{
    [Theory]
    [InlineData("launch_host", "hostApp")]
    [InlineData("open_model", "filePath")]
    [InlineData("read_file_info", "filePath")]
    public async Task WrongScalarKind_ReturnsMcpError_AndServerRemainsUsable(string toolName, string argumentName)
    {
        using var host = DaemonHostBuilder.CreateStdioHost([]);
        var engine = host.Services.GetRequiredService<McpEngine>();
        var pipeName = $"revitdevtool-daemon-binding-{Guid.NewGuid():N}";
        using var stop = new CancellationTokenSource();
        var serverTask = RunServerAsync(pipeName, engine.CreateServerOptions(), host.Services, stop.Token);

        await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(5000, TestContext.Current.CancellationToken);
        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(pipe, pipe, NullLoggerFactory.Instance),
            new McpClientOptions { ClientInfo = new Implementation { Name = "binding-test", Version = "1.0" } },
            cancellationToken: TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<McpException>(() => client.CallToolAsync(
            toolName,
            new Dictionary<string, object?> { [argumentName] = new[] { "wrong-kind" } },
            cancellationToken: TestContext.Current.CancellationToken).AsTask());

        var health = await client.CallToolAsync(
            "devtools_search",
            new Dictionary<string, object?> { ["limit"] = 1 },
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(health.IsError);

        stop.Cancel();
        try
        {
            await serverTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
        }
    }

    [Fact]
    public async Task OpenModel_StringHostId_ReturnsMcpError()
    {
        using var host = DaemonHostBuilder.CreateStdioHost([]);
        var engine = host.Services.GetRequiredService<McpEngine>();
        var pipeName = $"revitdevtool-daemon-hostid-{Guid.NewGuid():N}";
        using var stop = new CancellationTokenSource();
        var serverTask = RunServerAsync(pipeName, engine.CreateServerOptions(), host.Services, stop.Token);

        await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(5000, TestContext.Current.CancellationToken);
        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(pipe, pipe, NullLoggerFactory.Instance),
            new McpClientOptions { ClientInfo = new Implementation { Name = "hostid-test", Version = "1.0" } },
            cancellationToken: TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<McpException>(() => client.CallToolAsync(
            "open_model",
            new Dictionary<string, object?> { ["filePath"] = "C:\\missing.rvt", ["hostId"] = "1234" },
            cancellationToken: TestContext.Current.CancellationToken).AsTask());

        stop.Cancel();
        try
        {
            await serverTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
        }
    }

    private static async Task RunServerAsync(
        string pipeName,
        McpServerOptions options,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        await using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        await pipe.WaitForConnectionAsync(cancellationToken);
        await using var transport = new StreamServerTransport(pipe, pipe, pipeName, NullLoggerFactory.Instance);
        await using var server = McpServer.Create(transport, options, NullLoggerFactory.Instance, services);
        await server.RunAsync(cancellationToken);
    }
}
