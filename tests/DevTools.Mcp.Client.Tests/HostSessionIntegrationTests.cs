using System.Text.Json;
using DevTools.Mcp.Client;
using DevTools.Mcp.Client.Tests.Harness;
using DevTools.Mcp.Core.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Client.Tests;

[TestClass]
public sealed class HostSessionIntegrationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ConnectAsync_ListsResourcesAndSupportsPassthrough()
    {
        await using var host = await FakeMcpHostPipe.StartAsync(cancellationToken: TestContext.CancellationToken);

        var session = await HostSession.ConnectAsync(
            host.PipeName,
            "test-machine",
            NullLoggerFactory.Instance,
            NullLogger<HostSession>.Instance,
            TestContext.CancellationToken);

        Assert.IsTrue(session.IsConnected);
        Assert.AreEqual(host.PipeName, session.PipeName);
        Assert.AreEqual("Revit", session.Info.HostApp);
        Assert.AreEqual(Environment.ProcessId, session.Info.ProcessId);

        var call = await session.Client.CallToolAsync(
            "echo",
            new Dictionary<string, object?> { ["message"] = "hi" },
            cancellationToken: TestContext.CancellationToken);

        Assert.AreNotEqual(true, call.IsError);
        Assert.Contains("echo:hi", call.Content.OfType<TextContentBlock>().Select(c => c.Text));

        var direct = await session.ReadResourceAsync("revit://version", TestContext.CancellationToken);
        Assert.IsTrue(direct.Contents.OfType<TextResourceContents>().Any(c => c.Text == "2025"));

        var templated = await session.ReadResourceAsync(
            "revit://element/{id}",
            new Dictionary<string, JsonElement> { ["id"] = JsonSerializer.SerializeToElement("42") },
            TestContext.CancellationToken);
        Assert.IsTrue(templated.Contents.OfType<TextResourceContents>().Any(c => c.Text == "element-42"));

        await session.DisposeAsync();
        Assert.IsFalse(session.IsConnected);
    }

    [TestMethod]
    public async Task CallToolPassthroughAsync_SendsRawToolsCall()
    {
        await using var host = await FakeMcpHostPipe.StartAsync(cancellationToken: TestContext.CancellationToken);

        var session = await HostSession.ConnectAsync(
            host.PipeName,
            "test-machine",
            NullLoggerFactory.Instance,
            NullLogger<HostSession>.Instance,
            TestContext.CancellationToken);

        _ = await session.Client.ListToolsAsync(cancellationToken: TestContext.CancellationToken);

        var parameters = new CallToolRequestParams
        {
            Name = "echo",
            Arguments = new Dictionary<string, JsonElement>
            {
                ["message"] = JsonSerializer.SerializeToElement("passthrough"),
            },
        };
        McpProtocol.EnsureCurrentProtocolMeta(parameters);
        parameters.Meta!["io.modelcontextprotocol/clientCapabilities"] = new System.Text.Json.Nodes.JsonObject();

        var outcome = await session.CallToolPassthroughAsync(parameters, TestContext.CancellationToken);

        Assert.IsFalse(outcome.IsInputRequired);
        Assert.Contains("echo:passthrough", outcome.ToolResult!.Content.OfType<TextContentBlock>().Select(c => c.Text));
        await session.DisposeAsync();
    }
}
