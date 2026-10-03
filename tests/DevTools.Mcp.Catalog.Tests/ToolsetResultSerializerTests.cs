using System.Text.Json;
using DevTools.Mcp.Discovery;
using DevTools.Mcp.Catalog.Tests.Harness;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class ToolsetResultSerializerTests
{
    [TestMethod]
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
        var result = ResultBridge.ToHostCallToolResult(alcShaped, outputSchema);

        Assert.AreEqual(240, result.StructuredContent!.Value.GetProperty("count").GetInt32());
        Assert.Contains("Found 3 elements", McpToolInvoke.Text(result), StringComparison.Ordinal);
        Assert.HasCount(1, result.Content);
    }

    [TestMethod]
    public void ToInvocationResponse_MapsPlainObjectWithStructuredSchema()
    {
        var payload = new { moved_count = 2, failures = (string[]?)null };
        var outputSchema = JsonSerializer.SerializeToElement(new { type = "object" });

        var result = ResultBridge.ToHostCallToolResult(payload, outputSchema);

        Assert.AreEqual(2, result.StructuredContent!.Value.GetProperty("moved_count").GetInt32());
        Assert.Contains("moved_count", McpToolInvoke.Text(result), StringComparison.Ordinal);
    }

    [TestMethod]
    public void ToInvocationResponse_PreservesHostCallToolResult()
    {
        var original = new CallToolResult
        {
            Content = [new TextContentBlock { Text = "ok" }],
            StructuredContent = JsonDocument.Parse("{\"healthy\":true}").RootElement.Clone(),
        };

        var outputSchema = JsonSerializer.SerializeToElement(new { type = "object" });
        var result = ResultBridge.ToHostCallToolResult(original, outputSchema);

        Assert.AreEqual("ok", McpToolInvoke.Text(result));
        Assert.IsTrue(result.StructuredContent!.Value.GetProperty("healthy").GetBoolean());
    }

    [TestMethod]
    public void ToHostCallToolResult_HostContentBlock_IsKept()
    {
        var block = new ToolUseContentBlock
        {
            Name = "demo",
            Id = "call-1",
            Input = JsonSerializer.SerializeToElement(new { }),
        };

        var result = ResultBridge.ToHostCallToolResult(block, outputSchema: null);

        Assert.HasCount(1, result.Content);
        Assert.IsInstanceOfType<ToolUseContentBlock>(result.Content[0]);
    }
}
