using System.Text.Json;
using DevTools.Daemon.Mcp.Processes;
using DevTools.Mcp.Client.Tests.Harness;
using DevTools.Mcp.Core.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Client.Tests;

[TestClass]
public sealed class ProcessSessionIntegrationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ConnectAsync_ListsResourcesAndSupportsPassthrough()
    {
        await using var host = await FakeMcpHostPipe.StartAsync(cancellationToken: TestContext.CancellationToken);

        var session = await ProcessSession.ConnectAsync(
            host.PipeName,
            NullLoggerFactory.Instance,
            NullLogger<ProcessSession>.Instance,
            TestContext.CancellationToken);

        Assert.IsTrue(session.IsConnected);
        Assert.AreEqual(host.PipeName, session.PipeName);
        Assert.AreEqual("Revit", session.Info.HostApp);

        await using (session)
        {
            var tools = await session.Client.ListToolsAsync(cancellationToken: TestContext.CancellationToken);
            Assert.IsTrue(tools.Any(t => t.ProtocolTool.Name == "echo"));

            var outcome = await session.CallToolPassthroughAsync(
                new CallToolRequestParams { Name = "echo", Arguments = new Dictionary<string, JsonElement> { ["text"] = JsonSerializer.SerializeToElement("hi") } },
                TestContext.CancellationToken);
            Assert.IsInstanceOfType<CallToolResult>(outcome);
        }
    }
}
