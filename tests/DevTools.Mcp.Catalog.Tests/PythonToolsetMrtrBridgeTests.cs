using System.Text.Json;
using DevTools.Mcp.Adapter;
using DevTools.Execution.External.Mcp.Backends;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class PythonToolsetMrtrBridgeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [TestMethod]
    public void PayloadNormalizer_ArgumentsOnly_RemainsLegacyShape()
    {
        var json = PythonMcpToolBackend.WriteRequest(new CallToolRequestParams
        {
            Name = "stub",
            Arguments = new Dictionary<string, JsonElement>
            {
                ["category"] = JsonSerializer.SerializeToElement("Walls", JsonOptions),
            },
        });

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.AreEqual(JsonValueKind.Object, root.ValueKind);
        Assert.AreEqual("Walls", root.GetProperty("category").GetString());
        Assert.IsFalse(root.TryGetProperty("inputResponses", out _));
        Assert.IsFalse(root.TryGetProperty("requestState", out _));
    }

    [TestMethod]
    public void PayloadNormalizer_NullParams_ReturnsEmptyObject()
    {
        Assert.AreEqual("{}", PythonMcpToolBackend.WriteRequest(null));
    }

    [TestMethod]
    public void PayloadNormalizer_NoArguments_ReturnsEmptyObject()
    {
        Assert.AreEqual("{}", PythonMcpToolBackend.WriteRequest(new CallToolRequestParams { Name = "stub" }));
    }

    [TestMethod]
    public void PayloadNormalizer_IncludesInputResponsesAndRequestState()
    {
        var json = PythonMcpToolBackend.WriteRequest(new CallToolRequestParams
        {
            Name = "stub",
            Arguments = new Dictionary<string, JsonElement>
            {
                ["dryRun"] = JsonSerializer.SerializeToElement(true, JsonOptions),
            },
            InputResponses = new Dictionary<string, InputResponse>
            {
                ["confirm"] = InputResponse.FromElicitResult(new ElicitResult { Action = "accept" }),
            },
            RequestState = "round-1",
        });

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.IsTrue(root.GetProperty("arguments").GetProperty("dryRun").GetBoolean());
        Assert.IsTrue(root.TryGetProperty("inputResponses", out var responses));
        Assert.IsTrue(responses.GetProperty("confirm").TryGetProperty("action", out var action));
        Assert.AreEqual("accept", action.GetString());
        Assert.AreEqual("round-1", root.GetProperty("requestState").GetString());
    }

    [TestMethod]
    public void PayloadNormalizer_RequestStateOnly_UsesStructuredShape()
    {
        var json = PythonMcpToolBackend.WriteRequest(new CallToolRequestParams
        {
            Name = "stub",
            RequestState = "poll-only",
        });

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.AreEqual("poll-only", root.GetProperty("requestState").GetString());
        Assert.IsFalse(root.TryGetProperty("inputResponses", out _));
        Assert.IsFalse(root.TryGetProperty("arguments", out _));
    }

    [TestMethod]
    public void PayloadNormalizer_InputResponsesWithoutArguments_IncludesResponsesOnly()
    {
        var json = PythonMcpToolBackend.WriteRequest(new CallToolRequestParams
        {
            Name = "stub",
            InputResponses = new Dictionary<string, InputResponse>
            {
                ["confirm"] = InputResponse.FromElicitResult(new ElicitResult { Action = "decline" }),
            },
        });

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.IsTrue(root.TryGetProperty("inputResponses", out _));
        Assert.IsFalse(root.TryGetProperty("arguments", out _));
    }

    [TestMethod]
    public void ParseCallToolResult_StillParsesSuccessResult()
    {
        var expected = new CallToolResult
        {
            Content = [new TextContentBlock { Text = "ok" }],
        };
        var json = JsonSerializer.Serialize(expected, McpJsonUtilities.DefaultOptions);
        var actual = PythonMcpToolBackend.ReadToolResult(json);

        Assert.HasCount(1, actual.Content);
        Assert.AreEqual("ok", ((TextContentBlock)actual.Content[0]).Text);
    }

    [TestMethod]
    public void ParseCallToolResult_InputRequired_ThrowsWithRequestsAndState()
    {
        var inputRequired = new InputRequiredResult
        {
            InputRequests = new Dictionary<string, InputRequest>
            {
                ["confirm"] = InputRequest.ForElicitation(new ElicitRequestParams { Message = "Delete?" }),
            },
            RequestState = "demo-state",
        };
        var json = JsonSerializer.Serialize(inputRequired, McpJsonUtilities.DefaultOptions);

        var ex = Assert.ThrowsExactly<InputRequiredException>(() => PythonMcpToolBackend.ReadToolResult(json));

        Assert.IsNotNull(ex.Result.InputRequests);
        Assert.Contains("confirm", ex.Result.InputRequests!.Keys);
        Assert.AreEqual("demo-state", ex.Result.RequestState);
        Assert.AreEqual("input_required", ex.Result.ResultType);
    }

    [TestMethod]
    public void ParseCallToolResult_RequestStateOnly_ThrowsInputRequiredException()
    {
        var inputRequired = new InputRequiredResult { RequestState = "state-only" };
        var json = JsonSerializer.Serialize(inputRequired, McpJsonUtilities.DefaultOptions);

        var ex = Assert.ThrowsExactly<InputRequiredException>(() => PythonMcpToolBackend.ReadToolResult(json));

        Assert.IsNull(ex.Result.InputRequests);
        Assert.AreEqual("state-only", ex.Result.RequestState);
    }

    [TestMethod]
    public void ParseCallToolResult_MalformedInputRequired_ThrowsClearError()
    {
        const string json = """{"resultType":"input_required","inputRequests":"not-a-map"}""";

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => PythonMcpToolBackend.ReadToolResult(json));
        Assert.Contains("malformed", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.IsNotNull(ex.InnerException);
    }

    [TestMethod]
    public void ParseCallToolResult_StructuredContentOnly_IsCallToolResult()
    {
        const string json = """{"structuredContent":{"ok":true}}""";

        var actual = PythonMcpToolBackend.ReadToolResult(json);
        Assert.IsTrue(actual.StructuredContent.HasValue);
        Assert.AreEqual(JsonValueKind.True, actual.StructuredContent.Value.GetProperty("ok").ValueKind);
    }
}
