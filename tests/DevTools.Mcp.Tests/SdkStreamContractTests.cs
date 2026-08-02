using System.IO.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Tests;

public class SdkStreamContractTests
{
    [Fact]
    public async Task InMemorySdk_InitializeListCallResourceAndErrors()
    {
        await using var harness = await SdkTestHarness.StartAsync(
            tools:
            [
                McpServerTool.Create(
                    (string message) => $"echo:{message}",
                    new McpServerToolCreateOptions
                    {
                        Name = "echo",
                        Description = "Echo tool"
                    }),
                McpServerTool.Create(
                    () =>
                    {
                        throw new InvalidOperationException("boom");
#pragma warning disable CS0162
                        return "";
#pragma warning restore CS0162
                    },
                    new McpServerToolCreateOptions
                    {
                        Name = "fail",
                        Description = "Always fails"
                    })
            ],
            resources:
            [
                McpServerResource.Create(
                    () => new TextResourceContents { Uri = "revit://version", Text = "2025", MimeType = "text/plain" },
                    new McpServerResourceCreateOptions { UriTemplate = "revit://version" }),
                McpServerResource.Create(
                    (string id) => new TextResourceContents
                    {
                        Uri = $"revit://element/{id}",
                        Text = $"element-{id}",
                        MimeType = "text/plain"
                    },
                    new McpServerResourceCreateOptions { UriTemplate = "revit://element/{id}" })
            ]);

        var client = harness.Client;

        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains(tools, t => t.Name == "echo");
        Assert.Contains(tools, t => t.Name == "fail");

        var call = await client.CallToolAsync(
            "echo",
            new Dictionary<string, object?> { ["message"] = "hi" },
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotEqual(true, call.IsError);
        Assert.Contains("echo:hi", call.Content.OfType<TextContentBlock>().Select(c => c.Text));

        var fail = await client.CallToolAsync("fail", cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(fail.IsError == true);

        var resources = await client.ListResourcesAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains(resources, r => r.Uri == "revit://version");

        var direct = await client.ReadResourceAsync("revit://version", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains(direct.Contents.OfType<TextResourceContents>(), c => c.Text == "2025");

        var templates = await client.ListResourceTemplatesAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains(templates, t => t.UriTemplate == "revit://element/{id}");

        var templated = await client.ReadResourceAsync(
            "revit://element/{id}",
            new Dictionary<string, object?> { ["id"] = "42" },
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains(templated.Contents.OfType<TextResourceContents>(), c => c.Text == "element-42");
    }

    [Fact]
    public async Task InMemorySdk_PropagatesCancellation()
    {
        await using var harness = await SdkTestHarness.StartAsync(
        [
            McpServerTool.Create(
                async (CancellationToken ct) =>
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                    return "done";
                },
                new McpServerToolCreateOptions { Name = "slow", Description = "Slow tool" })
        ]);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await harness.Client.CallToolAsync("slow", cancellationToken: cts.Token));
    }
}

internal sealed class SdkTestHarness : IAsyncDisposable
{
    private readonly Pipe _clientToServer;
    private readonly Pipe _serverToClient;
    private readonly CancellationTokenSource _cts;
    private readonly Task _serverTask;
    private readonly McpServer _server;
    private readonly ServiceProvider _appServices;

    private SdkTestHarness(
        McpClient client,
        McpServer server,
        Task serverTask,
        Pipe clientToServer,
        Pipe serverToClient,
        CancellationTokenSource cts,
        ServiceProvider appServices)
    {
        Client = client;
        _server = server;
        _serverTask = serverTask;
        _clientToServer = clientToServer;
        _serverToClient = serverToClient;
        _cts = cts;
        _appServices = appServices;
    }

    public McpClient Client { get; }

    public static async Task<SdkTestHarness> StartAsync(
        IEnumerable<McpServerTool>? tools = null,
        IEnumerable<McpServerResource>? resources = null)
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        var cts = new CancellationTokenSource();

        var toolCollection = new McpServerPrimitiveCollection<McpServerTool>();
        foreach (var tool in tools ?? [])
            toolCollection.TryAdd(tool);

        var resourceCollection = new McpServerResourceCollection();
        foreach (var resource in resources ?? [])
            resourceCollection.TryAdd(resource);

        var options = new McpServerOptions
        {
            ServerInfo = new Implementation { Name = "test-host", Version = "1.0.0" },
            ToolCollection = toolCollection,
            ResourceCollection = resourceCollection,
            Capabilities = new ServerCapabilities
            {
                Tools = new ToolsCapability { ListChanged = true },
                Resources = new ResourcesCapability { ListChanged = true }
            }
        };

        var transport = new StreamServerTransport(
            clientToServer.Reader.AsStream(),
            serverToClient.Writer.AsStream(),
            "test-server",
            NullLoggerFactory.Instance);
        var appServices = TestMcpAppServices.Create();
        var server = McpServer.Create(transport, options, NullLoggerFactory.Instance, appServices);
        var serverTask = server.RunAsync(cts.Token);

        var client = await McpClient.CreateAsync(
            new StreamClientTransport(
                clientToServer.Writer.AsStream(),
                serverToClient.Reader.AsStream(),
                NullLoggerFactory.Instance),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: TestContext.Current.CancellationToken);

        return new SdkTestHarness(client, server, serverTask, clientToServer, serverToClient, cts, appServices);
    }

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
        await _cts.CancelAsync();
        _clientToServer.Writer.Complete();
        _serverToClient.Writer.Complete();
        try { await _serverTask; } catch { /* ignored */ }
        await _server.DisposeAsync();
        _cts.Dispose();
        _appServices.Dispose();
    }
}
