using System.Text.Json;
using DevTools.Mcp.Core.Protocol;

namespace DevTools.Mcp.Tests.Host.Conformance;

/// <summary>Golden JSON shape checks for host spec writers (Phase 5).</summary>
public sealed class McpHostConformanceTests
{
    [Fact]
    public void ToolsList_MatchesGoldenEnvelope()
    {
        var json = CatalogListEncoder.Tools(
        [
            new McpToolDescriptor
            {
                Name = "ping",
                Title = "Ping",
                InputSchema = JsonSerializer.SerializeToElement(new { type = "object", properties = new { } }),
            },
        ]);

        Assert.Equal(
            """
            {"tools":[{"name":"ping","title":"Ping","inputSchema":{"type":"object","properties":{}}}]}
            """.Trim(),
            json.ToJsonString());
    }

    [Fact]
    public void ResourcesList_MatchesGoldenEnvelope()
    {
        var json = CatalogListEncoder.Resources(
        [
            new McpResourceDescriptor
            {
                Uri = "sample://status",
                Name = "status",
                MimeType = "text/plain",
            },
        ]);

        Assert.Equal(
            """
            {"resources":[{"uri":"sample://status","name":"status","mimeType":"text/plain"}]}
            """.Trim(),
            json.ToJsonString());
    }

    [Fact]
    public void ToolCallResult_MatchesGoldenTextContent()
    {
        var json = InvocationResponseEncoder.ToNode(new McpInvocationResponse
        {
            Content = [new McpTextContent("ok")],
        });

        Assert.Equal(
            """{"content":[{"type":"text","text":"ok"}]}""",
            json.ToJsonString());
    }

    [Fact]
    public void ResourceRead_MatchesGoldenTextContent()
    {
        var json = ReadResourceEncoder.ToNode(new McpReadResourceResponse
        {
            Contents =
            [
                new McpReadResourceTextContent("sample://status", "ok", "text/plain"),
            ],
        });

        Assert.Equal(
            """{"contents":[{"uri":"sample://status","mimeType":"text/plain","text":"ok"}]}""",
            json.ToJsonString());
    }

    [Fact]
    public void ResourceTemplatesList_MatchesGoldenEnvelope()
    {
        var json = CatalogListEncoder.ResourceTemplates(
        [
            new McpResourceTemplateDescriptor
            {
                Name = "view",
                UriTemplate = "sample://views/{viewId}",
                MimeType = "application/json",
            },
        ]);

        Assert.Equal(
            """
            {"resourceTemplates":[{"uriTemplate":"sample://views/{viewId}","name":"view","mimeType":"application/json"}]}
            """.Trim(),
            json.ToJsonString());
    }
}
