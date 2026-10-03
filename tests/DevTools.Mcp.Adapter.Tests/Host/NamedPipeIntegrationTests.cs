using System.IO.Pipes;
using DevTools.Mcp.Adapter.Tests.Harness;
using DevTools.Mcp.Hosting;
using DevTools.Mcp.Isolation;
using DevTools.Mcp.Transport;
using DevTools.Mcp.Core.Catalog;
using DevTools.Mcp.Core.Models;
using DevTools.Execution.Abstractions;
using DevTools.Settings;
using DevTools.Settings.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Moq;

namespace DevTools.Mcp.Adapter.Tests.Host;

[TestClass]
public sealed class NamedPipeIntegrationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task NamedPipe_McpClientTalksToHostHandler()
    {
        var pipeName = HostPipeName.FormatMcp("TestHost", Guid.NewGuid().ToString("N")[..8], Environment.ProcessId);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(20));

        var (options, _, _, appServices) = McpHostTestHarness.CreateServerOptionsWithTool("ping", "pong", "Ping");

        using var serverPipe = NamedPipes.Open(pipeName);
        var acceptTask = serverPipe.WaitForConnectionAsync(cts.Token);

        using var clientPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await clientPipe.ConnectAsync(cts.Token);
        await acceptTask;

        await using var endpoint = PipeEndpoint.Create(
            serverPipe,
            options,
            appServices,
            "test-host");
        var serverTask = endpoint.RunAsync(cts.Token);

        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(clientPipe, clientPipe, NullLoggerFactory.Instance),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: cts.Token);

        var listed = await client.ListToolsAsync(cancellationToken: cts.Token);
        Assert.IsTrue(listed.Any(tool => tool.Name == "ping"));

        var result = await client.CallToolAsync("ping", cancellationToken: cts.Token);
        Assert.AreNotEqual(true, result.IsError);
        Assert.Contains("pong", result.Content.OfType<TextContentBlock>().Select(block => block.Text));

        await client.DisposeAsync();
        await cts.CancelAsync();
        try { await serverTask; } catch { /* ignored */ }
        appServices.Dispose();
    }

    [TestMethod]
    public async Task NamedPipe_ClientReceivesToolListChangedNotification()
    {
        var pipeName = HostPipeName.FormatMcp("TestHost", Guid.NewGuid().ToString("N")[..8], Environment.ProcessId);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(20));

        var (options, source, _, appServices) = McpHostTestHarness.CreateServerOptionsWithTool("ping", "pong", "Ping");
        options.ProtocolVersion = "2025-11-25";
        var hostContext = appServices.GetRequiredService<IHostContextExecutor>();
        var toolsetContexts = new McpToolsetContextManager(NullLogger<McpToolsetContextManager>.Instance);

        using var serverPipe = NamedPipes.Open(pipeName);
        var acceptTask = serverPipe.WaitForConnectionAsync(cts.Token);

        using var clientPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await clientPipe.ConnectAsync(cts.Token);
        await acceptTask;

        await using var endpoint = PipeEndpoint.Create(
            serverPipe,
            options,
            appServices,
            "test-host");
        var serverTask = endpoint.RunAsync(cts.Token);

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

        _ = await client.ListToolsAsync(cancellationToken: cts.Token);

        var loader = new Mock<ICatalogLoader>();
        loader
            .Setup(l => l.LoadCatalog(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<IReadOnlyCollection<string>>()))
            .Returns(new RegistryCatalog
            {
                Tools =
                [
                    McpHostTestHarness.CreateRegisteredTool("ping"),
                    McpHostTestHarness.CreateRegisteredTool("added"),
                ],
                Resources = [],
            });
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.McpRegistryConfig).Returns(new McpRegistryConfig());
        var reloadedStore = new McpCatalogStore(loader.Object, settings.Object);
        reloadedStore.EnsureLoaded();

        McpServerCollections.RebuildCollections(
            reloadedStore,
            [source],
            hostContext,
            toolsetContexts,
            options.ToolCollection!,
            options.ResourceCollection!);

        await notificationReceived.Task.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);

        await client.DisposeAsync();
        await cts.CancelAsync();
        try { await serverTask; } catch { /* ignored */ }
        appServices.Dispose();
    }
}
