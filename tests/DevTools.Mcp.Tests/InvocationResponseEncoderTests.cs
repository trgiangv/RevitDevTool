using System.Text.Json;
using DevTools.Mcp.Adapter.Host;
using DevTools.Mcp.Core.Protocol;
using DevTools.Mcp.Core.Utils;
using DevTools.Mcp.Tests.Harness;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Tests;

public sealed class InvocationResponseEncoderTests
{
    [Fact]
    public void ToNode_WritesTextContentAndStructuredContent()
    {
        var response = new McpInvocationResponse
        {
            Content = [new McpTextContent("ok")],
            StructuredContent = JsonSerializer.SerializeToElement(new { healthy = true }),
        };

        var json = HostToolResultJson.ToNode(response).AsObject();

        Assert.Equal("ok", json["content"]!.AsArray()[0]!["text"]!.GetValue<string>());
        Assert.True(json["structuredContent"]!.AsObject()["healthy"]!.GetValue<bool>());
    }

    [Fact]
    public void ToNode_WritesIsErrorFlag()
    {
        var response = new McpInvocationResponse
        {
            IsError = true,
            Content = [new McpTextContent("failed")],
        };

        var json = HostToolResultJson.ToNode(response).AsObject();

        Assert.True(json["isError"]!.GetValue<bool>());
        Assert.Equal("failed", json["content"]![0]!["text"]!.GetValue<string>());
    }

    [Fact]
    public void ToNode_MatchesSdkShape_ForSimpleTextResult()
    {
        var sdk = new CallToolResult
        {
            Content = [new TextContentBlock { Text = "pong" }],
        };
        var core = new McpInvocationResponse
        {
            Content = [new McpTextContent("pong")],
        };

        var sdkJson = JsonSerializer.Serialize(sdk, ToolHelpers.ProtocolOptions);
        var json = HostToolResultJson.ToNode(core).ToJsonString();

        using var sdkDoc = JsonDocument.Parse(sdkJson);
        using var coreDoc = JsonDocument.Parse(json);
        Assert.Equal(
            sdkDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString(),
            coreDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public void ToNode_WritesAnnotationsWithCamelCaseKeys()
    {
        var response = new McpInvocationResponse
        {
            Content =
            [
                new McpTextContent("hi") { Annotations = new Annotations { Priority = 0.5f } },
            ],
        };

        var json = HostToolResultJson.ToNode(response).ToJsonString();

        using var doc = JsonDocument.Parse(json);
        var annotations = doc.RootElement.GetProperty("content")[0].GetProperty("annotations");
        Assert.True(annotations.TryGetProperty("priority", out var priority));
        Assert.Equal(0.5f, priority.GetSingle());
        Assert.False(annotations.TryGetProperty("Priority", out _));
    }

    [Fact]
    public void PrepareForWire_EmptyTextWithStructured_UsesPreview()
    {
        var response = new McpInvocationResponse
        {
            Content = [new McpTextContent(string.Empty)],
            StructuredContent = JsonSerializer.SerializeToElement(new { healthy = true, document = "Project1" }),
        };

        var prepared = InvocationResponseEncoder.PrepareForWire(response);

        Assert.Contains("healthy", McpToolInvoke.Text(prepared), StringComparison.Ordinal);
        Assert.False(string.IsNullOrEmpty(McpToolInvoke.Text(prepared)));
    }

    [Fact]
    public void PrepareForWire_EmptyTextWithoutStructured_DropsBlock()
    {
        var response = new McpInvocationResponse
        {
            Content = [new McpTextContent(string.Empty)],
        };

        var prepared = InvocationResponseEncoder.PrepareForWire(response);

        Assert.Empty(prepared.Content);
    }

    [Fact]
    public void ToNode_EmptyTextWithoutStructured_WritesEmptyContentArray()
    {
        var response = new McpInvocationResponse
        {
            Content = [new McpTextContent(string.Empty)],
        };

        var json = HostToolResultJson.ToNode(response).AsObject();

        Assert.Empty(json["content"]!.AsArray());
        Assert.DoesNotContain("{}", json.ToJsonString(), StringComparison.Ordinal);
    }
}
