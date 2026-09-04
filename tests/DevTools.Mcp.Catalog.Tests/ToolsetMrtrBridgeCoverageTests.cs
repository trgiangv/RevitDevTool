using System.Text.Json;
using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Core.Invocation;
using DevTools.Mcp.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Tests;

public sealed class ToolsetMrtrBridgeCoverageTests
{
    [Fact]
    public void TryGetInputRequiredResult_ReadsFromMeta_WhenFieldMissing()
    {
        var original = new InputRequiredException(requestState: "meta-round1");
        var response = new McpInvocationResponse
        {
            Meta = new System.Text.Json.Nodes.JsonObject
            {
                [McpTaskExecutionMeta.Invocation.InputRequired] =
                    System.Text.Json.Nodes.JsonNode.Parse(
                        JsonSerializer.Serialize(original.Result, ToolHelpers.ProtocolOptions))!,
            },
        };

        Assert.True(ToolsetMrtrBridge.TryGetInputRequiredResult(response, out var restored));
        Assert.Equal("meta-round1", restored!.RequestState);
    }

    [Fact]
    public void ToHostException_UsesMessage_WhenForeignResultMissing()
    {
        var foreign = new ExceptionWithoutResult("fallback-state");

        var host = ToolsetMrtrBridge.ToHostException(foreign);

        Assert.Equal("fallback-state", host.Result.RequestState);
    }

    [Fact]
    public void ToHostException_MapsNonGenericDictionaryInputRequests()
    {
        var foreign = new ForeignDictionaryMrtr.InputRequiredException(
            requestState: "dict-round1",
            inputRequests: new System.Collections.Hashtable
            {
                ["confirm"] = new ForeignDictionaryMrtr.InputRequest { Method = "elicitation/create" },
            });

        var host = ToolsetMrtrBridge.ToHostException(foreign);

        Assert.NotNull(host.Result.InputRequests);
        Assert.Equal("elicitation/create", host.Result.InputRequests!["confirm"].Method);
    }

    [Fact]
    public void ToHostException_ReturnsHostException_Unchanged()
    {
        var original = new InputRequiredException(requestState: "host");

        Assert.Same(original, ToolsetMrtrBridge.ToHostException(original));
    }

    private sealed class ExceptionWithoutResult(string message) : Exception(message)
    {
        public object? Result => null;
    }
}

file static class ForeignDictionaryMrtr
{
    public sealed class InputRequiredException : Exception
    {
        public InputRequiredException(string requestState, System.Collections.IDictionary inputRequests)
            : base("foreign")
        {
            Result = new InputRequiredResult
            {
                RequestState = requestState,
                InputRequests = inputRequests,
            };
        }

        public InputRequiredResult Result { get; }
    }

    public sealed class InputRequiredResult
    {
        public string? RequestState { get; set; }
        public System.Collections.IDictionary? InputRequests { get; set; }
    }

    public sealed class InputRequest
    {
        public string Method { get; set; } = "";
    }
}
