using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.Mcp.Core.Invocation;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Core.Tests;

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

        var json = SerializeForWire(response).AsObject();

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

        var json = SerializeForWire(response).AsObject();

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
        var json = SerializeForWire(core).ToJsonString();

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

        var json = SerializeForWire(response).ToJsonString();

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

        Assert.Contains("healthy", Text(prepared), StringComparison.Ordinal);
        Assert.False(string.IsNullOrEmpty(Text(prepared)));
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

        var json = SerializeForWire(response).AsObject();

        Assert.Empty(json["content"]!.AsArray());
        Assert.DoesNotContain("{}", json.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewStructured_LongPayload_TruncatesWithEllipsis()
    {
        var structured = JsonSerializer.SerializeToElement(new { payload = new string('x', 300) });

        var preview = InvocationResponseEncoder.PreviewStructured(structured);

        Assert.True(preview.Length <= 240);
        Assert.EndsWith("...", preview);
    }

    [Fact]
    public void PrepareForWire_StructuredOnly_AddsPreviewTextBlock()
    {
        var response = new McpInvocationResponse
        {
            StructuredContent = JsonSerializer.SerializeToElement(new { healthy = true }),
        };

        var prepared = InvocationResponseEncoder.PrepareForWire(response);

        Assert.Single(prepared.Content);
        Assert.Contains("healthy", Text(prepared), StringComparison.Ordinal);
    }

    private static JsonNode SerializeForWire(McpInvocationResponse response)
    {
        var prepared = InvocationResponseEncoder.PrepareForWire(response);
        var sdk = new CallToolResult
        {
            Content = prepared.Content.Select(ToSdk).ToList(),
            IsError = prepared.IsError,
            StructuredContent = prepared.StructuredContent?.Clone(),
            Meta = prepared.Meta?.DeepClone().AsObject(),
        };

        return JsonSerializer.SerializeToNode(sdk, ToolHelpers.ProtocolOptions)!;
    }

    private static ContentBlock ToSdk(McpContent content) => content switch
    {
        McpTextContent text => new TextContentBlock
        {
            Text = text.Text,
            Annotations = text.Annotations,
            Meta = text.Meta?.DeepClone().AsObject(),
        },
        _ => throw new NotSupportedException($"Unsupported content type '{content.GetType().FullName}'."),
    };

    private static string Text(McpInvocationResponse response) =>
        response.Content.OfType<McpTextContent>().Single().Text;
}
