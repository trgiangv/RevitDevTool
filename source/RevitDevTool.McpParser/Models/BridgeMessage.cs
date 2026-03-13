using System.Text.Json;
namespace RevitDevTool.McpParser.Models;

public sealed class BridgeMessage
{
    public const string TypeRequest = "request";
    public const string TypeResponse = "response";
    public const string TypeNotification = "notification";

    public string Type { get; init; } = string.Empty;
    public string? Id { get; init; }
    public string? Method { get; init; }
    public JsonElement? Params { get; init; }
    public JsonElement? Result { get; init; }
    public bool IsError { get; init; }
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
