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
    public void RoundTrip_PreservesText()
    {
        var original = new McpInvocationResponse
        {
            Content = [new McpTextContent("Model healthy, 0 selected")],
            StructuredContent = JsonSerializer.SerializeToElement(new { healthy = true }),
        };

        var roundTripped = SdkInvocationMapper.RoundTripCore(original);
        Assert.Equal(McpToolInvoke.Text(original), McpToolInvoke.Text(roundTripped));
    }

    [Fact]
    public void EnsureWireSafe_ReplacesNullText_FromStructuredContent()
    {
        var broken = new McpInvocationResponse
        {
            Content = [new McpTextContent(string.Empty)],
            StructuredContent = JsonSerializer.SerializeToElement(new { healthy = true, document = "Project1" }),
        };

        var fixedResult = ToolsetResultSerializer.EnsureWireSafe(broken);
        Assert.False(string.IsNullOrEmpty(McpToolInvoke.Text(fixedResult)));
        Assert.Contains("healthy", McpToolInvoke.Text(fixedResult), StringComparison.Ordinal);
    }
}
