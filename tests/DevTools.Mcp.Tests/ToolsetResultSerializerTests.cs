using System.Text.Json;
using DevTools.Mcp.Adapter.Bridging;
using DevTools.Mcp.Adapter;
using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Tests.Harness;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Tests;

public sealed class ToolsetResultSerializerTests
{
    [Fact]
    public void ToInvocationResponse_BridgesAlcCallToolResultJson()
    {
        var alcShaped = new
        {
            content = new[] { new { type = "text", text = "Found 3 elements (total 240, truncated=true, offset=0)" } },
            structuredContent = new
            {
                count = 240,
                truncated = true,
                elements = new[] { new { id = 1L, category = "Walls" } },
            },
        };

        var outputSchema = JsonSerializer.SerializeToElement(new { type = "object" });
        var result = ToolsetResultSerializer.ToInvocationResponse(alcShaped, outputSchema);

        Assert.Equal(240, result.StructuredContent!.Value.GetProperty("count").GetInt32());
        Assert.Contains("Found 3 elements", McpToolInvoke.Text(result), StringComparison.Ordinal);
        Assert.Single(result.Content);
    }

    [Fact]
    public void ToInvocationResponse_MapsPlainObjectWithStructuredSchema()
    {
        var payload = new { moved_count = 2, failures = (string[]?)null };
        var outputSchema = JsonSerializer.SerializeToElement(new { type = "object" });

        var result = ToolsetResultSerializer.ToInvocationResponse(payload, outputSchema);

        Assert.Equal(2, result.StructuredContent!.Value.GetProperty("moved_count").GetInt32());
        Assert.Contains("moved_count", McpToolInvoke.Text(result), StringComparison.Ordinal);
    }

    [Fact]
    public void ToInvocationResponse_PreservesHostCallToolResult()
    {
        var original = new CallToolResult
        {
            Content = [new TextContentBlock { Text = "ok" }],
            StructuredContent = JsonDocument.Parse("{\"healthy\":true}").RootElement.Clone(),
        };

        var outputSchema = JsonSerializer.SerializeToElement(new { type = "object" });
        var result = ToolsetResultSerializer.ToInvocationResponse(original, outputSchema);

        Assert.Equal("ok", McpToolInvoke.Text(result));
        Assert.True(result.StructuredContent!.Value.GetProperty("healthy").GetBoolean());
    }

    [Fact]
    public void ToSdk_SerializeDeserialize_PreservesText()
    {
        var original = new McpInvocationResponse
        {
            Content = [new McpTextContent("Model healthy, 0 selected")],
            StructuredContent = JsonSerializer.SerializeToElement(new { healthy = true }),
        };

        var sdk = SdkInvocationMapper.ToSdk(original);
        var roundTripped = JsonSerializer.Deserialize<CallToolResult>(
            JsonSerializer.Serialize(sdk, ToolHelpers.ProtocolOptions),
            ToolHelpers.ProtocolOptions)!;

        Assert.Equal(McpToolInvoke.Text(original), ((TextContentBlock)sdk.Content[0]).Text);
        Assert.Equal(((TextContentBlock)sdk.Content[0]).Text, ((TextContentBlock)roundTripped.Content[0]).Text);
    }

    [Fact]
    public void ToInvocationResponse_UnsupportedHostBlock_Throws()
    {
        var block = new ToolUseContentBlock
        {
            Name = "demo",
            Id = "call-1",
            Input = JsonSerializer.SerializeToElement(new { }),
        };

        var ex = Assert.Throws<NotSupportedException>(
            () => ToolsetResultSerializer.ToInvocationResponse(block, outputSchema: null));

        Assert.Contains("Unsupported host content block", ex.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ToolUseContentBlock), ex.Message, StringComparison.Ordinal);
    }
}
