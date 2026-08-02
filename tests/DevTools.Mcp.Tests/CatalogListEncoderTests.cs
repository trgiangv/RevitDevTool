using System.Text.Json;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Tests;

public sealed class CatalogListEncoderTests
{
    [Fact]
    public void Tools_WritesToolDescriptors()
    {
        var tools = new[]
        {
            new McpToolDescriptor
            {
                Name = "ping",
                Title = "Ping",
                Description = "Health check",
                InputSchema = JsonSerializer.SerializeToElement(new { type = "object" }),
                Annotations = new McpToolAnnotations
                {
                    ReadOnly = true,
                    Idempotent = true,
                    Title = "Ping",
                },
            },
        };

        var json = CatalogListEncoder.Tools(tools).AsObject();
        var item = json["tools"]!.AsArray()[0]!.AsObject();

        Assert.Equal("ping", item["name"]!.GetValue<string>());
        Assert.Equal("Ping", item["title"]!.GetValue<string>());
        Assert.Equal("Health check", item["description"]!.GetValue<string>());
        Assert.True(item["annotations"]!["readOnlyHint"]!.GetValue<bool>());
        Assert.True(item["annotations"]!["idempotentHint"]!.GetValue<bool>());
    }

    [Fact]
    public void Resources_WritesResourceDescriptors()
    {
        var resources = new[]
        {
            new McpResourceDescriptor
            {
                Uri = "sample://demo/status",
                Name = "demo_status",
                Title = "Demo Status",
                MimeType = "application/json",
                Size = 128,
                Annotations = new McpResourceAnnotations { Priority = 0.9 },
            },
        };

        var json = CatalogListEncoder.Resources(resources).AsObject();
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
        var descriptor = DescriptorFactory.FromTool(sdk);

        var sdkJson = JsonSerializer.Serialize(sdk, McpJsonUtilities.DefaultOptions);
        var json = CatalogListEncoder.Tool(descriptor).ToJsonString();

        using var sdkDoc = JsonDocument.Parse(sdkJson);
        using var coreDoc = JsonDocument.Parse(json);
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
        var descriptor = DescriptorFactory.FromResource(sdk);

        var sdkJson = JsonSerializer.Serialize(sdk, McpJsonUtilities.DefaultOptions);
        var json = CatalogListEncoder.Resource(descriptor).ToJsonString();

        using var sdkDoc = JsonDocument.Parse(sdkJson);
        using var coreDoc = JsonDocument.Parse(json);
        Assert.Equal(
            sdkDoc.RootElement.GetProperty("uri").GetString(),
            coreDoc.RootElement.GetProperty("uri").GetString());
        Assert.Equal(
            sdkDoc.RootElement.GetProperty("annotations").GetProperty("priority").GetDouble(),
            coreDoc.RootElement.GetProperty("annotations").GetProperty("priority").GetDouble(),
            3);
    }
}
