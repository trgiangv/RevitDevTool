using DevTools.Execution.Abstractions;
using DevTools.Hosting;
using DevTools.Mcp.Adapter.Tests.Harness;
using DevTools.Mcp.Core.Catalog;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Sessions;
using DevTools.Mcp.Hosting;
using DevTools.Mcp.Isolation;
using DevTools.Mcp.Transport;
using DevTools.Settings;
using DevTools.Settings.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Moq;
using System.Diagnostics;
using System.IO.Pipes;

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
        var toolsetContexts = new McpAssemblyContextManager(NullLogger<McpAssemblyContextManager>.Instance);

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

    [TestMethod]
    public async Task McpPipeServer_StopAsync_completes_with_an_idle_connected_client()
    {
        var version = Guid.NewGuid().ToString("N");
        var processId = Process.GetCurrentProcess().Id;
        var hostInfo = Mock.Of<IHostAppInfo>(info =>
            info.Host == HostApp.Revit
            && info.VersionNumber == version
            && info.ProcessId == processId);
        var (_, source, catalogStore, appServices) =
            McpHostTestHarness.CreateServerOptionsWithTool("ping");
        var connectTracker = new Mock<IConnectTracker>();
        var server = new McpPipeServer(
            catalogStore,
            [source],
            connectTracker.Object,
            new McpAssemblyContextManager(NullLogger<McpAssemblyContextManager>.Instance),
            appServices.GetRequiredService<IHostContextExecutor>(),
            hostInfo,
            appServices,
            NullLogger<McpPipeServer>.Instance);
        var pipeName = HostPipeName.FormatMcp(hostInfo.Host.ToString(), version, processId);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await server.StartAsync(timeout.Token);

        using var clientPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await clientPipe.ConnectAsync(timeout.Token);
        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(clientPipe, clientPipe, NullLoggerFactory.Instance),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: timeout.Token);

        var stopTask = Task.Factory.StartNew(
            () =>
            {
                SynchronizationContext.SetSynchronizationContext(new NonPumpingSynchronizationContext());
                try
                {
                    server.StopAsync(timeout.Token).GetAwaiter().GetResult();
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(null);
                }
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        var completed = await Task.WhenAny(stopTask, Task.Delay(TimeSpan.FromSeconds(5), TestContext.CancellationToken));
        Assert.AreSame(stopTask, completed, "Host MCP shutdown captured the blocked host UI synchronization context.");
        await stopTask;
        connectTracker.Verify(tracker => tracker.Reset(), Times.Once);

        await client.DisposeAsync();
        appServices.Dispose();
        server.Dispose();
    }

    private sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state)
        {
            // A CAD shutdown callback blocks its UI thread instead of pumping posted work.
        }
    }
}
