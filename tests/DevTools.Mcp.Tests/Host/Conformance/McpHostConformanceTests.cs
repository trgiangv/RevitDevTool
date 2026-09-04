using System.Text.Json;
using DevTools.Mcp.Adapter.Host;
using DevTools.Mcp.Core.Protocol;
using DevTools.Mcp.Core.Protocol.Invocation;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Tests.Host.Conformance;

/// <summary>Golden JSON shape checks for host spec writers (Phase 5).</summary>
public sealed class McpHostConformanceTests
{
    [Fact]
    public void ToolsList_MatchesGoldenEnvelope()
    {
        var json = JsonSerializer.SerializeToNode(
            new ListToolsResult
            {
                Tools =
                [
                    new Tool
                    {
                        Name = "ping",
                        Title = "Ping",
                        InputSchema = JsonSerializer.SerializeToElement(new { type = "object", properties = new { } }),
                    },
                ],
            },
            McpJsonUtilities.DefaultOptions)!;

        Assert.Equal(
            """
            {"tools":[{"name":"ping","title":"Ping","inputSchema":{"type":"object","properties":{}}}]}
            """.Trim(),
            json.ToJsonString());
    }

    [Fact]
    public void ResourcesList_MatchesGoldenEnvelope()
    {
        var json = JsonSerializer.SerializeToNode(
            new ListResourcesResult
            {
                Resources =
                [
                    new Resource
                    {
                        Uri = "sample://status",
                        Name = "status",
                        MimeType = "text/plain",
                    },
                ],
            },
            McpJsonUtilities.DefaultOptions)!;

        using var doc = JsonDocument.Parse(json.ToJsonString());
        var item = doc.RootElement.GetProperty("resources")[0];
        Assert.Equal("sample://status", item.GetProperty("uri").GetString());
        Assert.Equal("status", item.GetProperty("name").GetString());
        Assert.Equal("text/plain", item.GetProperty("mimeType").GetString());
    }

    [Fact]
    public void ToolCallResult_MatchesGoldenTextContent()
    {
        var json = HostToolResultJson.ToNode(new McpInvocationResponse
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
        var json = JsonSerializer.SerializeToNode(
            new ReadResourceResult
            {
                Contents =
                [
                    new TextResourceContents
                    {
                        Uri = "sample://status",
                        MimeType = "text/plain",
                        Text = "ok",
                    },
                ],
            },
            McpJsonUtilities.DefaultOptions)!;

        Assert.Equal(
            """{"contents":[{"uri":"sample://status","mimeType":"text/plain","text":"ok"}]}""",
            json.ToJsonString());
    }

    [Fact]
    public void ResourceTemplatesList_MatchesGoldenEnvelope()
    {
        var json = JsonSerializer.SerializeToNode(
            new ListResourceTemplatesResult
            {
                ResourceTemplates =
                [
                    new ResourceTemplate
                    {
                        Name = "view",
                        UriTemplate = "sample://views/{viewId}",
                        MimeType = "application/json",
                    },
                ],
            },
            McpJsonUtilities.DefaultOptions)!;

        using var doc = JsonDocument.Parse(json.ToJsonString());
        var item = doc.RootElement.GetProperty("resourceTemplates")[0];
        Assert.Equal("sample://views/{viewId}", item.GetProperty("uriTemplate").GetString());
        Assert.Equal("view", item.GetProperty("name").GetString());
        Assert.Equal("application/json", item.GetProperty("mimeType").GetString());
    }
}
