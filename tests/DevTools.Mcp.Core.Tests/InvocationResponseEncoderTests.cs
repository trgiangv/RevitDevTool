using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.Mcp.Core.Invocation;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Core.Tests;

[TestClass]
public sealed class InvocationResponseEncoderTests
{
    [TestMethod]
    public void ToNode_WritesTextContentAndStructuredContent()
    {
        var response = new McpInvocationResponse
        {
            Content = [new McpTextContent("ok")],
            StructuredContent = JsonSerializer.SerializeToElement(new { healthy = true }),
        };

        var json = SerializeForWire(response).AsObject();

        Assert.AreEqual("ok", json["content"]!.AsArray()[0]!["text"]!.GetValue<string>());
        Assert.IsTrue(json["structuredContent"]!.AsObject()["healthy"]!.GetValue<bool>());
    }

    [TestMethod]
    public void ToNode_WritesIsErrorFlag()
    {
        var response = new McpInvocationResponse
        {
            IsError = true,
            Content = [new McpTextContent("failed")],
        };

        var json = SerializeForWire(response).AsObject();

        Assert.IsTrue(json["isError"]!.GetValue<bool>());
        Assert.AreEqual("failed", json["content"]![0]!["text"]!.GetValue<string>());
    }

    [TestMethod]
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
        Assert.AreEqual(
            sdkDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString(),
            coreDoc.RootElement.GetProperty("content")[0].GetProperty("text").GetString());
    }

    [TestMethod]
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
        Assert.IsTrue(annotations.TryGetProperty("priority", out var priority));
        Assert.AreEqual(0.5f, priority.GetSingle());
        Assert.IsFalse(annotations.TryGetProperty("Priority", out _));
    }

    [TestMethod]
    public void PrepareForWire_EmptyTextWithStructured_UsesPreview()
    {
        var response = new McpInvocationResponse
        {
            Content = [new McpTextContent(string.Empty)],
            StructuredContent = JsonSerializer.SerializeToElement(new { healthy = true, document = "Project1" }),
        };

        var prepared = InvocationResponseEncoder.PrepareForWire(response);

        Assert.Contains("healthy", Text(prepared), StringComparison.Ordinal);
        Assert.IsFalse(string.IsNullOrEmpty(Text(prepared)));
    }

    [TestMethod]
    public void PrepareForWire_EmptyTextWithoutStructured_DropsBlock()
    {
        var response = new McpInvocationResponse
        {
            Content = [new McpTextContent(string.Empty)],
        };

        var prepared = InvocationResponseEncoder.PrepareForWire(response);

        Assert.IsEmpty(prepared.Content);
    }

    [TestMethod]
    public void ToNode_EmptyTextWithoutStructured_WritesEmptyContentArray()
    {
        var response = new McpInvocationResponse
        {
            Content = [new McpTextContent(string.Empty)],
        };

        var json = SerializeForWire(response).AsObject();

        Assert.IsEmpty(json["content"]!.AsArray());
        Assert.DoesNotContain("{}", json.ToJsonString(), StringComparison.Ordinal);
    }

    [TestMethod]
    public void PreviewStructured_LongPayload_TruncatesWithEllipsis()
    {
        var structured = JsonSerializer.SerializeToElement(new { payload = new string('x', 300) });

        var preview = InvocationResponseEncoder.PreviewStructured(structured);

        Assert.IsTrue(preview.Length <= 240);
        Assert.EndsWith("...", preview);
    }

    [TestMethod]
    public void PrepareForWire_StructuredOnly_AddsPreviewTextBlock()
    {
        var response = new McpInvocationResponse
        {
            StructuredContent = JsonSerializer.SerializeToElement(new { healthy = true }),
        };

        var prepared = InvocationResponseEncoder.PrepareForWire(response);

        Assert.HasCount(1, prepared.Content);
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
