using System.Text;
using System.Text.Json;
using DevTools.Mcp.Adapter.Host;
using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Core.Protocol;
using DevTools.Mcp.Core.Utils;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Tests;

public sealed class HostToolResultJsonTests
{
    [Fact]
    public void ToNode_InputRequired_MatchesCanonicalSdkJson()
    {
        var elicitParams = JsonSerializer.SerializeToElement(new
        {
            message = "Confirm?",
            requestedSchema = new
            {
                type = "object",
                properties = new { ok = new { type = "boolean" } },
                required = new[] { "ok" },
            },
        });

        var inputRequired = new InputRequiredResult
        {
            RequestState = "demo-round1",
            InputRequests = new Dictionary<string, InputRequest>
            {
                ["confirm"] = new InputRequest
                {
                    Method = "elicitation/create",
                    Params = elicitParams,
                },
            },
        };

        var expected = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(inputRequired, ToolHelpers.ProtocolOptions));

        var response = ToolsetMrtrBridge.ToInputRequiredResponse(
            new InputRequiredException(
                inputRequests: inputRequired.InputRequests,
                requestState: inputRequired.RequestState));

        var actual = Encoding.UTF8.GetBytes(
            HostToolResultJson.ToNode(response).ToJsonString());

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ToNode_StandardResponse_EncodesAsCallToolResult()
    {
        var response = new McpInvocationResponse
        {
            Content = [new McpTextContent("ok")],
        };

        var json = HostToolResultJson.ToNode(response).AsObject();

        Assert.False(json.ContainsKey(McpSpecKeys.ResultType.Key));
        Assert.Equal("ok", json["content"]!.AsArray()[0]!["text"]!.GetValue<string>());
    }
}
