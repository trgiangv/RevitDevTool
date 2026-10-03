using System.Text.Json;
using DevTools.Mcp;
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
    public void PayloadNormalizer_ArgumentsOnly_UsesStructuredShape()
    {
        var json = PythonSource.WriteRequest(new CallToolRequestParams
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
        Assert.AreEqual("Walls", root.GetProperty("arguments").GetProperty("category").GetString());
        Assert.IsFalse(root.TryGetProperty("inputResponses", out _));
        Assert.IsFalse(root.TryGetProperty("requestState", out _));
    }

    [TestMethod]
    public void PayloadNormalizer_NullParams_ReturnsEmptyObject()
    {
        Assert.AreEqual("{}", PythonSource.WriteRequest(null));
    }

    [TestMethod]
    public void PayloadNormalizer_NoArguments_ReturnsEmptyObject()
    {
        Assert.AreEqual("{}", PythonSource.WriteRequest(new CallToolRequestParams { Name = "stub" }));
    }

    [TestMethod]
    public void PayloadNormalizer_DropsInputResponsesAndRequestState()
    {
        var json = PythonSource.WriteRequest(new CallToolRequestParams
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
        Assert.IsFalse(root.TryGetProperty("inputResponses", out _));
        Assert.IsFalse(root.TryGetProperty("requestState", out _));
    }

    [TestMethod]
    public void PayloadNormalizer_RequestStateOnly_ReturnsEmptyObject()
    {
        var json = PythonSource.WriteRequest(new CallToolRequestParams
        {
            Name = "stub",
            RequestState = "poll-only",
        });

        Assert.AreEqual("{}", json);
    }

    [TestMethod]
    public void PayloadNormalizer_InputResponsesWithoutArguments_ReturnsEmptyObject()
    {
        var json = PythonSource.WriteRequest(new CallToolRequestParams
        {
            Name = "stub",
            InputResponses = new Dictionary<string, InputResponse>
            {
                ["confirm"] = InputResponse.FromElicitResult(new ElicitResult { Action = "decline" }),
            },
        });

        Assert.AreEqual("{}", json);
    }

    [TestMethod]
    public void ParseCallToolResult_StillParsesSuccessResult()
    {
        var expected = new CallToolResult
        {
            Content = [new TextContentBlock { Text = "ok" }],
        };
        var json = JsonSerializer.Serialize(expected, McpJsonUtilities.DefaultOptions);
        var actual = PythonSource.ReadToolResult(json);

        Assert.HasCount(1, actual.Content);
        Assert.AreEqual("ok", ((TextContentBlock)actual.Content[0]).Text);
    }

    [TestMethod]
    public void ParseCallToolResult_InputRequired_ReturnsHostError()
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

        var result = PythonSource.ReadToolResult(json);

        Assert.IsTrue(result.IsError);
        Assert.Contains("not supported", Assert.IsInstanceOfType<TextContentBlock>(result.Content.Single()).Text, StringComparison.Ordinal);
    }

    [TestMethod]
    public void ParseCallToolResult_RequestStateOnly_ReturnsHostError()
    {
        var inputRequired = new InputRequiredResult { RequestState = "state-only" };
        var json = JsonSerializer.Serialize(inputRequired, McpJsonUtilities.DefaultOptions);

        var result = PythonSource.ReadToolResult(json);

        Assert.IsTrue(result.IsError);
    }

    [TestMethod]
    public void ParseCallToolResult_MalformedInputRequired_ReturnsHostError()
    {
        const string json = """{"resultType":"input_required","inputRequests":"not-a-map"}""";

        var result = PythonSource.ReadToolResult(json);

        Assert.IsTrue(result.IsError);
    }

    [TestMethod]
    public void ParseCallToolResult_StructuredContentOnly_IsCallToolResult()
    {
        const string json = """{"structuredContent":{"ok":true}}""";

        var actual = PythonSource.ReadToolResult(json);
        Assert.IsTrue(actual.StructuredContent.HasValue);
        Assert.AreEqual(JsonValueKind.True, actual.StructuredContent.Value.GetProperty("ok").ValueKind);
    }
}
