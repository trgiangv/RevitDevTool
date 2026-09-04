using System.Text.Json;
using DevTools.Mcp.Client;
using DevTools.Mcp.Client.Tests.Harness;
using DevTools.Mcp.Core.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Client.Tests;

public sealed class HostSessionIntegrationTests
{
    [Fact]
    public async Task ConnectAsync_ListsResourcesAndSupportsPassthrough()
    {
        await using var host = await FakeMcpHostPipe.StartAsync(cancellationToken: TestContext.Current.CancellationToken);

        var session = await HostSession.ConnectAsync(
            host.PipeName,
            "test-machine",
            NullLoggerFactory.Instance,
            NullLogger<HostSession>.Instance,
            TestContext.Current.CancellationToken);

        Assert.True(session.IsConnected);
        Assert.Equal(host.PipeName, session.PipeName);
        Assert.Equal("Revit", session.Info.HostApp);
        Assert.Equal(Environment.ProcessId, session.Info.ProcessId);

        var call = await session.Client.CallToolAsync(
            "echo",
            new Dictionary<string, object?> { ["message"] = "hi" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEqual(true, call.IsError);
        Assert.Contains("echo:hi", call.Content.OfType<TextContentBlock>().Select(c => c.Text));

        var direct = await session.ReadResourceAsync("revit://version", TestContext.Current.CancellationToken);
        Assert.Contains(direct.Contents.OfType<TextResourceContents>(), c => c.Text == "2025");

        var templated = await session.ReadResourceAsync(
            "revit://element/{id}",
            new Dictionary<string, JsonElement> { ["id"] = JsonSerializer.SerializeToElement("42") },
            TestContext.Current.CancellationToken);
        Assert.Contains(templated.Contents.OfType<TextResourceContents>(), c => c.Text == "element-42");

        await session.DisposeAsync();
        Assert.False(session.IsConnected);
    }

    [Fact]
    public async Task CallToolPassthroughAsync_SendsRawToolsCall()
    {
        await using var host = await FakeMcpHostPipe.StartAsync(cancellationToken: TestContext.Current.CancellationToken);

        var session = await HostSession.ConnectAsync(
            host.PipeName,
            "test-machine",
            NullLoggerFactory.Instance,
            NullLogger<HostSession>.Instance,
            TestContext.Current.CancellationToken);

        _ = await session.Client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

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

        var outcome = await session.CallToolPassthroughAsync(parameters, TestContext.Current.CancellationToken);

        Assert.False(outcome.IsInputRequired);
        Assert.Contains("echo:passthrough", outcome.ToolResult!.Content.OfType<TextContentBlock>().Select(c => c.Text));
        await session.DisposeAsync();
    }
}
