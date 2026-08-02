using System.Text.Json;
using System.Text.Json.Nodes;

using DevTools.Mcp.Core.Protocol;

using JsonRpcKeys = DevTools.Mcp.Core.Protocol.McpSpecKeys.JsonRpc;



namespace DevTools.Mcp.Adapter.Host;



/// <summary>JSON-RPC 2.0 envelope helpers for the host MCP handler.</summary>

internal static class McpJsonRpc

{

    public const string Version = JsonRpcKeys.Version;

    public const int InvalidRequest = JsonRpcKeys.InvalidRequest;

    public const int MethodNotFound = JsonRpcKeys.MethodNotFound;

    public const int InvalidParams = JsonRpcKeys.InvalidParams;

    public const int InternalError = JsonRpcKeys.InternalError;

    public const int UnsupportedProtocolVersion = JsonRpcKeys.UnsupportedProtocolVersion;



    public static JsonObject CreateSuccess(JsonNode? id, JsonNode result) =>

        new()

        {

            [JsonRpcKeys.Envelope] = Version,

            [JsonRpcKeys.Id] = id?.DeepClone(),

            [JsonRpcKeys.Result] = result.DeepClone(),

        };



    public static JsonObject CreateError(JsonNode? id, int code, string message) =>
        CreateError(id, code, message, data: null);

    public static JsonObject CreateError(JsonNode? id, int code, string message, JsonObject? data) =>
        new()
        {
            [JsonRpcKeys.Envelope] = Version,
            [JsonRpcKeys.Id] = id?.DeepClone(),
            [JsonRpcKeys.Error] = data is null
                ? new JsonObject
                {
                    [JsonRpcKeys.Code] = code,
                    [JsonRpcKeys.Message] = message,
                }
                : new JsonObject
                {
                    [JsonRpcKeys.Code] = code,
                    [JsonRpcKeys.Message] = message,
                    ["data"] = data.DeepClone(),
                },
        };



    public static bool TryGetMethod(JsonObject request, out string? method)

    {

        if (request[JsonRpcKeys.Method] is JsonValue value && value.TryGetValue(out string? parsed))

        {

            method = parsed;

            return true;

        }



        method = null;

        return false;

    }



    public static JsonNode? GetId(JsonObject request) => request[JsonRpcKeys.Id];



    public static bool HasId(JsonObject request) => request.ContainsKey(JsonRpcKeys.Id);



    public static JsonObject? GetParams(JsonObject request) =>

        request[JsonRpcKeys.Params] switch

        {

            JsonObject obj => obj,

            null => null,

            _ => null,

        };



    public static JsonObject ParseRequest(string json) =>

        JsonNode.Parse(json)?.AsObject()

        ?? throw new JsonException("Expected a JSON-RPC request object.");



    public static string Serialize(JsonObject message) =>

        message.ToJsonString(new JsonSerializerOptions { WriteIndented = false });

}

