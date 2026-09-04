using System.Text.Json;
using DevTools.Mcp.Adapter;
using DevTools.Execution.External.Mcp.Backends;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Tests;

public sealed class PythonToolsetMrtrBridgeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
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
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal("Walls", root.GetProperty("category").GetString());
        Assert.False(root.TryGetProperty("inputResponses", out _));
        Assert.False(root.TryGetProperty("requestState", out _));
    }

    [Fact]
    public void PayloadNormalizer_NullParams_ReturnsEmptyObject()
    {
        Assert.Equal("{}", PythonMcpToolBackend.WriteRequest(null));
    }

    [Fact]
    public void PayloadNormalizer_NoArguments_ReturnsEmptyObject()
    {
        Assert.Equal("{}", PythonMcpToolBackend.WriteRequest(new CallToolRequestParams { Name = "stub" }));
    }

    [Fact]
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
        Assert.True(root.GetProperty("arguments").GetProperty("dryRun").GetBoolean());
        Assert.True(root.TryGetProperty("inputResponses", out var responses));
        Assert.True(responses.GetProperty("confirm").TryGetProperty("action", out var action));
        Assert.Equal("accept", action.GetString());
        Assert.Equal("round-1", root.GetProperty("requestState").GetString());
    }

    [Fact]
    public void PayloadNormalizer_RequestStateOnly_UsesStructuredShape()
    {
        var json = PythonMcpToolBackend.WriteRequest(new CallToolRequestParams
        {
            Name = "stub",
            RequestState = "poll-only",
        });

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("poll-only", root.GetProperty("requestState").GetString());
        Assert.False(root.TryGetProperty("inputResponses", out _));
        Assert.False(root.TryGetProperty("arguments", out _));
    }

    [Fact]
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
        Assert.True(root.TryGetProperty("inputResponses", out _));
        Assert.False(root.TryGetProperty("arguments", out _));
    }

    [Fact]
    public void ParseCallToolResult_StillParsesSuccessResult()
    {
        var expected = new CallToolResult
        {
            Content = [new TextContentBlock { Text = "ok" }],
        };
        var json = JsonSerializer.Serialize(expected, McpJsonUtilities.DefaultOptions);
        var actual = PythonMcpToolBackend.ReadToolResult(json);

        Assert.Equal("ok", ((TextContentBlock)Assert.Single(actual.Content)).Text);
    }

    [Fact]
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

        var ex = Assert.Throws<InputRequiredException>(() => PythonMcpToolBackend.ReadToolResult(json));

        Assert.NotNull(ex.Result.InputRequests);
        Assert.Contains("confirm", ex.Result.InputRequests!.Keys);
        Assert.Equal("demo-state", ex.Result.RequestState);
        Assert.Equal("input_required", ex.Result.ResultType);
    }

    [Fact]
    public void ParseCallToolResult_RequestStateOnly_ThrowsInputRequiredException()
    {
        var inputRequired = new InputRequiredResult { RequestState = "state-only" };
        var json = JsonSerializer.Serialize(inputRequired, McpJsonUtilities.DefaultOptions);

        var ex = Assert.Throws<InputRequiredException>(() => PythonMcpToolBackend.ReadToolResult(json));

        Assert.Null(ex.Result.InputRequests);
        Assert.Equal("state-only", ex.Result.RequestState);
    }

    [Fact]
    public void ParseCallToolResult_MalformedInputRequired_ThrowsClearError()
    {
        const string json = """{"resultType":"input_required","inputRequests":"not-a-map"}""";

        var ex = Assert.Throws<InvalidOperationException>(() => PythonMcpToolBackend.ReadToolResult(json));
        Assert.Contains("malformed", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public void ParseCallToolResult_StructuredContentOnly_IsCallToolResult()
    {
        const string json = """{"structuredContent":{"ok":true}}""";

        var actual = PythonMcpToolBackend.ReadToolResult(json);
        Assert.True(actual.StructuredContent.HasValue);
        Assert.Equal(JsonValueKind.True, actual.StructuredContent.Value.GetProperty("ok").ValueKind);
    }
}
