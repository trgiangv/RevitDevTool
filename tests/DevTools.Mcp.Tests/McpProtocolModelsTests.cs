using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.Mcp.Core.Protocol;
using DevTools.Mcp.Core.Protocol.Invocation;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Tests;

public sealed class McpProtocolModelsTests
{
    [Fact]
    public void SdkTool_RoundTrips_ThroughSdkJsonOptions()
    {
        var descriptor = new Tool
        {
            Name = "get_demo_status",
            Title = "Get Demo Status",
            Description = "Return demo status.",
            InputSchema = JsonSerializer.SerializeToElement(new { type = "object" }),
            Annotations = new ToolAnnotations { IdempotentHint = true, OpenWorldHint = false },
        };

        var json = JsonSerializer.Serialize(descriptor, McpJsonUtilities.DefaultOptions);
        var roundTrip = JsonSerializer.Deserialize<Tool>(json, McpJsonUtilities.DefaultOptions);

        Assert.NotNull(roundTrip);
        Assert.Equal(descriptor.Name, roundTrip.Name);
        Assert.Equal(descriptor.Title, roundTrip.Title);
        Assert.True(roundTrip.Annotations?.IdempotentHint);
    }

    [Fact]
    public void InvocationRequestReader_Deserializes_MrtrFields()
    {
        var parameters = JsonNode.Parse(
            """
            {
                "name": "get_demo_status",
                "arguments": { "topic": "demo" },
                "inputResponses": { "confirm": { "action": "accept" } },
                "requestState": "round-2"
            }
            """)!.AsObject();

        var request = InvocationRequestReader.FromWire(parameters);

        Assert.Equal("get_demo_status", request.Name);
        Assert.NotNull(request.Arguments);
        Assert.Equal("demo", request.Arguments["topic"].GetString());
        Assert.NotNull(request.InputResponses);
        Assert.Contains("confirm", request.InputResponses.Keys);
        var elicitResult = request.InputResponses["confirm"].Deserialize(InputResponse.ElicitResultJsonTypeInfo);
        Assert.Equal("accept", elicitResult?.Action);
        Assert.Equal("round-2", request.RequestState);
    }

    [Fact]
    public void InvocationRequestReader_Deserializes_ProgressToken_FromMetaString()
    {
        var parameters = JsonNode.Parse(
            """
            {
                "name": "demo",
                "_meta": { "progressToken": "token-42" }
            }
            """)!.AsObject();

        var request = InvocationRequestReader.FromWire(parameters);

        Assert.NotNull(request.ProgressToken);
        Assert.Equal("token-42", request.ProgressToken.Value.ToString());
        Assert.Equal("token-42", request.Meta!["progressToken"]!.GetValue<string>());
    }

    [Fact]
    public void InvocationRequestReader_Deserializes_ProgressToken_FromMetaNumber()
    {
        var parameters = JsonNode.Parse(
            """
            {
                "name": "demo",
                "_meta": { "progressToken": 42 }
            }
            """)!.AsObject();

        var request = InvocationRequestReader.FromWire(parameters);

        Assert.NotNull(request.ProgressToken);
        Assert.Equal(42L, request.ProgressToken.Value.Token);
        Assert.Equal(42, request.Meta!["progressToken"]!.GetValue<long>());
    }

    [Fact]
    public void InvocationRequestReader_Ignores_TopLevelProgressToken()
    {
        var parameters = JsonNode.Parse(
            """
            {
                "name": "demo",
                "progressToken": 42
            }
            """)!.AsObject();

        var request = InvocationRequestReader.FromWire(parameters);

        Assert.Null(request.ProgressToken);
        Assert.Null(request.Meta);
    }

    [Fact]
    public void InvocationRequestReader_NullOrEmptyParams_ReturnsEmptyRequest()
    {
        var empty = InvocationRequestReader.FromWire(new JsonObject());
        Assert.Equal(string.Empty, empty.Name);
        Assert.Null(empty.Arguments);

        var nullParams = InvocationRequestReader.FromWire(null);
        Assert.Equal(string.Empty, nullParams.Name);
        Assert.Null(nullParams.Arguments);
    }
}
