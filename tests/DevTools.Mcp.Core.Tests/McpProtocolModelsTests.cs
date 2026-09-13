using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.Mcp.Core.Protocol;
using DevTools.Mcp.Core.Protocol.Invocation;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Core.Tests;

[TestClass]
public sealed class McpProtocolModelsTests
{
    [TestMethod]
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

        Assert.IsNotNull(roundTrip);
        Assert.AreEqual(descriptor.Name, roundTrip.Name);
        Assert.AreEqual(descriptor.Title, roundTrip.Title);
        Assert.IsTrue(roundTrip.Annotations?.IdempotentHint);
    }

    [TestMethod]
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

        Assert.AreEqual("get_demo_status", request.Name);
        Assert.IsNotNull(request.Arguments);
        Assert.AreEqual("demo", request.Arguments["topic"].GetString());
        Assert.IsNotNull(request.InputResponses);
        Assert.Contains("confirm", request.InputResponses.Keys);
        var elicitResult = request.InputResponses["confirm"].Deserialize(InputResponse.ElicitResultJsonTypeInfo);
        Assert.AreEqual("accept", elicitResult?.Action);
        Assert.AreEqual("round-2", request.RequestState);
    }

    [TestMethod]
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

        Assert.IsNotNull(request.ProgressToken);
        Assert.AreEqual("token-42", request.ProgressToken.Value.ToString());
        Assert.AreEqual("token-42", request.Meta!["progressToken"]!.GetValue<string>());
    }

    [TestMethod]
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

        Assert.IsNotNull(request.ProgressToken);
        Assert.AreEqual(42L, request.ProgressToken.Value.Token);
        Assert.AreEqual(42, request.Meta!["progressToken"]!.GetValue<long>());
    }

    [TestMethod]
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

        Assert.IsNull(request.ProgressToken);
        Assert.IsNull(request.Meta);
    }

    [TestMethod]
    public void InvocationRequestReader_NullOrEmptyParams_ReturnsEmptyRequest()
    {
        var empty = InvocationRequestReader.FromWire(new JsonObject());
        Assert.AreEqual(string.Empty, empty.Name);
        Assert.IsNull(empty.Arguments);

        var nullParams = InvocationRequestReader.FromWire(null);
        Assert.AreEqual(string.Empty, nullParams.Name);
        Assert.IsNull(nullParams.Arguments);
    }
}
