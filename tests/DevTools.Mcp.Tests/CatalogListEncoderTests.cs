using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Tests;

public sealed class CatalogListEncoderTests
{
    [Fact]
    public void ListToolsResult_SerializesToolDescriptors()
    {
        var tools = new List<Tool>
        {
            new()
            {
                Name = "ping",
                Title = "Ping",
                Description = "Health check",
                InputSchema = JsonSerializer.SerializeToElement(new { type = "object" }),
                Annotations = new ToolAnnotations
                {
                    ReadOnlyHint = true,
                    IdempotentHint = true,
                    Title = "Ping",
                },
            },
        };

        var json = JsonSerializer.SerializeToNode(new ListToolsResult { Tools = tools }, McpJsonUtilities.DefaultOptions)!.AsObject();
        var item = json["tools"]!.AsArray()[0]!.AsObject();

        Assert.Equal("ping", item["name"]!.GetValue<string>());
        Assert.Equal("Ping", item["title"]!.GetValue<string>());
        Assert.Equal("Health check", item["description"]!.GetValue<string>());
        Assert.True(item["annotations"]!["readOnlyHint"]!.GetValue<bool>());
        Assert.True(item["annotations"]!["idempotentHint"]!.GetValue<bool>());
    }

    [Fact]
    public void ListResourcesResult_SerializesResourceDescriptors()
    {
        var resources = new List<Resource>
        {
            new()
            {
                Uri = "sample://demo/status",
                Name = "demo_status",
                Title = "Demo Status",
                MimeType = "application/json",
                Size = 128,
                Annotations = new Annotations { Priority = 0.9f },
            },
        };

        var json = JsonSerializer.SerializeToNode(new ListResourcesResult { Resources = resources }, McpJsonUtilities.DefaultOptions)!.AsObject();
        var item = json["resources"]!.AsArray()[0]!.AsObject();

        Assert.Equal("sample://demo/status", item["uri"]!.GetValue<string>());
        Assert.Equal("demo_status", item["name"]!.GetValue<string>());
        Assert.Equal(128, item["size"]!.GetValue<long>());
        Assert.Equal(0.9, item["annotations"]!["priority"]!.GetValue<double>(), 3);
    }

    [Fact]
    public void Tool_MatchesSdkShape_ForSimpleTool()
    {
        var sdk = new Tool
        {
            Name = "get_status",
            Title = "Get Status",
            Description = "Returns status",
            InputSchema = JsonSerializer.SerializeToElement(new { type = "object" }),
            Annotations = new ToolAnnotations
            {
                ReadOnlyHint = true,
                Title = "Get Status",
            },
        };

        var sdkJson = JsonSerializer.Serialize(sdk, McpJsonUtilities.DefaultOptions);
        var listJson = JsonSerializer.Serialize(new ListToolsResult { Tools = [sdk] }, McpJsonUtilities.DefaultOptions);
        using var listDoc = JsonDocument.Parse(listJson);
        var coreJson = listDoc.RootElement.GetProperty("tools")[0].GetRawText();

        using var sdkDoc = JsonDocument.Parse(sdkJson);
        using var coreDoc = JsonDocument.Parse(coreJson);
        Assert.Equal(
            sdkDoc.RootElement.GetProperty("name").GetString(),
            coreDoc.RootElement.GetProperty("name").GetString());
        Assert.Equal(
            sdkDoc.RootElement.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean(),
            coreDoc.RootElement.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
    }

    [Fact]
    public void Resource_MatchesSdkShape_ForDirectResource()
    {
        var sdk = new Resource
        {
            Uri = "sample://demo/status",
            Name = "demo_status",
            Title = "Demo Status",
            MimeType = "application/json",
            Size = 64,
            Annotations = new Annotations { Priority = 0.5f },
        };

        var sdkJson = JsonSerializer.Serialize(sdk, McpJsonUtilities.DefaultOptions);
        var listJson = JsonSerializer.Serialize(new ListResourcesResult { Resources = [sdk] }, McpJsonUtilities.DefaultOptions);
        using var listDoc = JsonDocument.Parse(listJson);
        var coreJson = listDoc.RootElement.GetProperty("resources")[0].GetRawText();

        using var sdkDoc = JsonDocument.Parse(sdkJson);
        using var coreDoc = JsonDocument.Parse(coreJson);
        Assert.Equal(
            sdkDoc.RootElement.GetProperty("uri").GetString(),
            coreDoc.RootElement.GetProperty("uri").GetString());
        Assert.Equal(
            sdkDoc.RootElement.GetProperty("annotations").GetProperty("priority").GetDouble(),
            coreDoc.RootElement.GetProperty("annotations").GetProperty("priority").GetDouble(),
            3);
    }

    [Fact]
    public void CoerceInputSchema_InvalidSchema_FallsBackToDefaultObject()
    {
        var invalid = JsonSerializer.SerializeToElement(new { type = "string" });
        var coerced = DescriptorFactory.CoerceInputSchema(invalid);

        Assert.Equal(JsonValueKind.Object, coerced.ValueKind);
        Assert.Equal("object", coerced.GetProperty("type").GetString());
    }

    [Fact]
    public void NormalizeTool_InvalidInputSchema_DoesNotThrow()
    {
        var tool = new Tool
        {
            Name = "safe_tool",
            InputSchema = DescriptorFactory.CoerceInputSchema(JsonSerializer.SerializeToElement(new { type = "array" })),
        };

        var normalized = DescriptorFactory.NormalizeTool(tool);

        Assert.Equal("safe_tool", normalized.Name);
        Assert.Equal("object", normalized.InputSchema.GetProperty("type").GetString());
    }
}
