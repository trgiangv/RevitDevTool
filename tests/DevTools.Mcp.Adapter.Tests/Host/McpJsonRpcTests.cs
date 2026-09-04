using System.Text.Json.Nodes;
using DevTools.Mcp.Adapter.Host;

namespace DevTools.Mcp.Adapter.Tests.Host;

public sealed class McpJsonRpcTests
{
    [Fact]
    public void RequestEnvelope_RoundTrips_ThroughParseAndSerialize()
    {
        const string json = """
            {"jsonrpc":"2.0","id":42,"method":"tools/list","params":{"cursor":"abc"}}
            """;

        var request = McpJsonRpc.ParseRequest(json);
        var serialized = McpJsonRpc.Serialize(request);
        var roundTrip = McpJsonRpc.ParseRequest(serialized);

        Assert.Equal("2.0", roundTrip["jsonrpc"]!.GetValue<string>());
        Assert.Equal(42, roundTrip["id"]!.GetValue<int>());
        Assert.Equal("tools/list", roundTrip["method"]!.GetValue<string>());
        Assert.Equal("abc", roundTrip["params"]!["cursor"]!.GetValue<string>());
    }

    [Fact]
    public void SuccessEnvelope_RoundTrips_WithResultPayload()
    {
        var result = new JsonObject
        {
            ["tools"] = new JsonArray
            {
                new JsonObject { ["name"] = "ping", ["description"] = "Ping" },
            },
        };

        var response = McpJsonRpc.CreateSuccess(7, result);
        var serialized = McpJsonRpc.Serialize(response);
        var roundTrip = McpJsonRpc.ParseRequest(serialized);

        Assert.Equal("2.0", roundTrip["jsonrpc"]!.GetValue<string>());
        Assert.Equal(7, roundTrip["id"]!.GetValue<int>());
        Assert.Equal("ping", roundTrip["result"]!["tools"]![0]!["name"]!.GetValue<string>());
        Assert.Null(roundTrip["error"]);
    }

    [Fact]
    public void ErrorEnvelope_RoundTrips_WithCodeAndMessage()
    {
        var response = McpJsonRpc.CreateError("req-1", ModelContextProtocol.McpErrorCode.MethodNotFound, "Method not found: foo");
        var serialized = McpJsonRpc.Serialize(response);
        var roundTrip = McpJsonRpc.ParseRequest(serialized);

        Assert.Equal("req-1", roundTrip["id"]!.GetValue<string>());
        Assert.Equal((int)ModelContextProtocol.McpErrorCode.MethodNotFound, roundTrip["error"]!["code"]!.GetValue<int>());
        Assert.Equal("Method not found: foo", roundTrip["error"]!["message"]!.GetValue<string>());
        Assert.Null(roundTrip["result"]);
    }

    [Fact]
    public void NotificationEnvelope_HasNoId_AndSerializesMethodOnly()
    {
        var notification = McpJsonRpc.CreateNotification("notifications/initialized");

        var serialized = McpJsonRpc.Serialize(notification);
        var roundTrip = McpJsonRpc.ParseRequest(serialized);

        Assert.False(McpJsonRpc.HasId(roundTrip));
        Assert.Equal("notifications/initialized", roundTrip["method"]!.GetValue<string>());
    }

    [Fact]
    public void CreateNotification_RoundTrips_WithoutId()
    {
        var notification = McpJsonRpc.CreateNotification(
            "notifications/tools/list_changed",
            new JsonObject { ["cursor"] = "abc" });
        var serialized = McpJsonRpc.Serialize(notification);
        var roundTrip = McpJsonRpc.ParseRequest(serialized);

        Assert.False(McpJsonRpc.HasId(roundTrip));
        Assert.Equal("notifications/tools/list_changed", roundTrip["method"]!.GetValue<string>());
        Assert.Equal("abc", roundTrip["params"]!["cursor"]!.GetValue<string>());
    }
}
