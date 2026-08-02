using System.Text.Json;
using DevTools.Mcp.Core.Protocol;

namespace DevTools.Mcp.Tests;

public sealed class McpProtocolModelsTests
{
    [Fact]
    public void McpToolDescriptor_RoundTrips_ThroughJsonContext()
    {
        var descriptor = new McpToolDescriptor
        {
            Name = "get_demo_status",
            Title = "Get Demo Status",
            Description = "Return demo status.",
            InputSchema = JsonSerializer.SerializeToElement(new { type = "object" }),
            Annotations = new McpToolAnnotations { Idempotent = true, OpenWorld = false },
        };

        var json = JsonSerializer.Serialize(descriptor, McpProtocolJsonContext.Default.McpToolDescriptor);
        var roundTrip = JsonSerializer.Deserialize(json, McpProtocolJsonContext.Default.McpToolDescriptor);

        Assert.NotNull(roundTrip);
        Assert.Equal(descriptor.Name, roundTrip.Name);
        Assert.Equal(descriptor.Title, roundTrip.Title);
        Assert.True(roundTrip.Annotations?.Idempotent);
    }

    [Fact]
    public void McpInvocationRequest_Serializes_MrtrFields()
    {
        var request = new McpInvocationRequest
        {
            Arguments = JsonSerializer.SerializeToElement(new { topic = "demo" }),
            InputResponses = new Dictionary<string, JsonElement>
            {
                ["confirm"] = JsonSerializer.SerializeToElement(true),
            },
            RequestState = JsonSerializer.SerializeToElement(new { step = 2 }),
            ProgressToken = 42,
        };

        var json = JsonSerializer.Serialize(request, McpProtocolJsonContext.Default.McpInvocationRequest);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal("demo", doc.RootElement.GetProperty("arguments").GetProperty("topic").GetString());
        Assert.True(doc.RootElement.GetProperty("inputResponses").GetProperty("confirm").GetBoolean());
        Assert.Equal(42, doc.RootElement.GetProperty("progressToken").GetInt64());
    }
}
