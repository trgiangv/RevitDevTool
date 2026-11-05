#pragma warning disable MCPEXP001

using DevTools.Mcp.Adapter.Tests.Harness;
using DevTools.Mcp.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.IO.Pipelines;

namespace DevTools.Mcp.Adapter.Tests;

[TestClass]
public sealed class HostSessionTasksTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SessionOptions_KeepTasksHandlers_SoATaskCallCanBePolled()
    {
        var (shared, _, _, services) = McpHostTestHarness.CreateServerOptionsWithTool("host_ping", "pong");
        using var _ = services;
        var options = McpServerCollections.CreateSessionOptions(shared);

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        await using var server = McpServer.Create(
            new StreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream(), "host-session", NullLoggerFactory.Instance),
            options,
            NullLoggerFactory.Instance,
            services);
        var serverTask = server.RunAsync(cts.Token);

        await using (var client = await McpClient.CreateAsync(
                         new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream(), NullLoggerFactory.Instance),
                         loggerFactory: NullLoggerFactory.Instance,
                         cancellationToken: cts.Token))
        {
            var request = new CallToolRequestParams { Name = "host_ping" };
            var augmented = await client.CallToolAsTaskAsync(request, cancellationToken: cts.Token);
            var polled = await client.CallToolWithPollingAsync(request, cancellationToken: cts.Token);

            Assert.IsTrue(augmented.IsTask);
            Assert.AreEqual("pong", polled.Content.OfType<TextContentBlock>().Single().Text);
        }

        await cts.CancelAsync();
        clientToServer.Writer.Complete();
        serverToClient.Writer.Complete();
        try { await serverTask; } catch (OperationCanceledException) { }
    }
}
