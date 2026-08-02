using System.Text.Json;
using DevTools.Mcp.Core.Protocol;
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

        var json = InvocationResponseEncoder.ToNode(response).AsObject();

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

        var json = InvocationResponseEncoder.ToNode(response).AsObject();

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

        var sdkJson = JsonSerializer.Serialize(sdk, McpJsonUtilities.DefaultOptions);
        var json = InvocationResponseEncoder.ToNode(core).ToJsonString();

        using var sdkDoc = JsonDocument.Parse(sdkJson);
        using var coreDoc = JsonDocument.Parse(json);
        Assert.Equal(
            sdkDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString(),
            coreDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString());
    }
}
