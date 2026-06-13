using System.Text.Json;
using System.Text.Json.Serialization;
namespace DevTools.McpParser.Models;

public sealed class BridgeMessage
{
    public const string TypeRequest = "request";
    public const string TypeResponse = "response";
    public const string TypeNotification = "notification";

    [JsonPropertyName(McpPropertyNames.Type)]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName(McpPropertyNames.Id)]
    public string? Id { get; init; }

    [JsonPropertyName(McpPropertyNames.Method)]
    public string? Method { get; init; }

    [JsonPropertyName(McpPropertyNames.Params)]
    public JsonElement? Params { get; init; }

    [JsonPropertyName(McpPropertyNames.Result)]
    public JsonElement? Result { get; init; }

    [JsonPropertyName(McpPropertyNames.IsError)]
    public bool IsError { get; init; }

    [JsonPropertyName(McpPropertyNames.ErrorMessage)]
    public string? ErrorMessage { get; init; }

    public static BridgeMessage Request(string id, string method, JsonElement? @params = null) =>
        new() { Type = TypeRequest, Id = id, Method = method, Params = @params };

    public static BridgeMessage Response(string id, JsonElement? result) =>
        new() { Type = TypeResponse, Id = id, Result = result };

    public static BridgeMessage Error(string id, string message) =>
        new() { Type = TypeResponse, Id = id, IsError = true, ErrorMessage = message };

    public static BridgeMessage Notification(string method, JsonElement? @params = null) =>
        new() { Type = TypeNotification, Method = method, Params = @params };
}
